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
	/// How far from the companion's <b>eye</b> a piece may be placed. Vanilla is the game's own
	/// <c>Player.m_maxPlaceDistance</c> (5 m, decompiled 1.0.20); the player may raise it in the F8 panel
	/// (<c>[Building] PlaceReach</c>, 5..20, 10 by default - "it is a mod, it does not have to be realistic", request of
	/// 03/11/2026). The flat cap used to be 2,5 m and could not reach the upper roof row of the house (y = 5, i.e. 4,9 m
	/// above the feet) - the companion walked in circles until the order timed out (player report, 01/11/2026: "parece
	/// que não alcança").
	///
	/// <para>It is read from the config on every use (a property, not a const): a value changed in the F8 panel applies to
	/// the orders that are already running, and a missing entry falls back to the vanilla 5 m.</para>
	/// </summary>
	private static float MaxPlace
	{
		get
		{
			return ModConfig.BuildPlaceReach != null ? ModConfig.BuildPlaceReach.Value : 5f;
		}
	}
	/// <summary>E7 (review, 03/11/2026): the walk still stops at <see cref="Reach"/>; the flat cap opens to
	/// <c>MaxPlace - 0,5</c> only after 3 s without getting closer, and the eye distance is always <see cref="MaxPlace"/>
	/// (vanilla 5 m, configurable up to 20 m).</summary>

	/// <summary>
	/// 11/11/2026 (Run 13): <c>[Building] ReachWalk</c> - walk to the nearest surface the piece can be reached <b>from</b>
	/// (a finished floor or roof row) instead of walking at the piece in mid-air, where the path snap starts the route far
	/// from the feet and the companion walks the wrong way. Read on every use like <see cref="MaxPlace"/>; a missing entry
	/// answers the switch's default (on).
	/// </summary>
	private static bool ReachWalk
	{
		get
		{
			return ModConfig.BuildReachWalk == null || ModConfig.BuildReachWalk.Value;
		}
	}

	/// <summary>
	/// 11/11/2026 (Run 14): <c>[Building] SpreadWorkers</c> - a shared site is not walked in a line: each worker takes a
	/// piece of the next few of its run that is <b>far</b> from what the others are building (<see cref="BuildSpread"/>).
	/// Read on every use like <see cref="ReachWalk"/>; a missing entry answers the switch's default (on).
	/// </summary>
	private static bool SpreadWorkers
	{
		get
		{
			return ModConfig.BuildSpreadWorkers == null || ModConfig.BuildSpreadWorkers.Value;
		}
	}

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

	/// <summary>
	/// 11/11/2026 (Run 13, the ridge of the <c>CommHomeBasicBP</c>): how far <b>down</b> the surfaces a piece can be placed
	/// from are looked for (<see cref="WalkSpot"/>). The measured house has its top roof row at 48,8 and the ground at ~38,
	/// so 12 m covers the whole climb in one look; it is the total of the ray, not of a step.
	/// </summary>
	private const float WalkReach = 12f;

	/// <summary>11/11/2026 (Run 13): the radii around the piece's own column that are sampled for those surfaces (the
	/// surface directly under the piece is <c>r = 0</c>).
	/// <para>11/11/2026 (Run 14): the measured ridge had the row it is placed from <b>beside</b> it, not under it - the ray
	/// down its own column went through the gable and reached the floor of the storey below (walk spot 41,9 for a piece at
	/// 48,8). The rings are 1,5 m and 3 m now (the rows of a roof/gable are 2 m apart, so 3 m reaches the neighbour row and
	/// the floor the plan's ladders lead to): <b>17 rays</b> per piece (the column + 8 + 8).</para></summary>
	private static readonly float[] s_walkRadii = { 1.5f, 3f };

	/// <summary>11/11/2026 (Run 14): how many hits one sample column may walk <b>through</b> before it gives up. A column
	/// whose first hit is not a surface - a <b>body</b> of a companion walking over the sample, a piece above the one being
	/// placed, furniture - used to lose the column entirely: the ray stops on the first collider, and with 5 companions on the
	/// roof that was most of the ring. The ray goes down past up to this many of them and takes the first surface it can
	/// stand on.</summary>
	private const int WalkHits = 3;

	/// <summary>11/11/2026 (Run 14): the surfaces of the piece's columns - ONE list reused and the keys already noted (a
	/// plane hit by two rings is one surface).</summary>
	private static readonly HashSet<int> s_walkSeen = new HashSet<int>();

	/// <summary>11/11/2026 (Run 13): the surfaces found under the current piece - ONE list reused (the spot is chosen once
	/// per piece, but a plan has 1000 of them).</summary>
	private static readonly List<Vector3> s_walkSurfaces = new List<Vector3>();

	/// <summary>
	/// 11/11/2026 (Run 14, the step aside): the <b>pending</b> pieces of the site around the piece being placed - ONE list
	/// reused (<c>MHR_BuildGroup.PendingNear</c>, asked once per piece, never per frame).
	/// </summary>
	private static readonly List<Vector3> s_asidePending = new List<Vector3>();

	/// <summary>11/11/2026 (Run 14, the step aside): the spots a companion may step to, in the order they are tried
	/// (<see cref="BuildAsideSpot.Plan"/>: away from the placer's front first, then its sides).</summary>
	private static readonly Vector3[] s_asideSpots = new Vector3[BuildAsideSpot.Ring];

	/// <summary>
	/// 11/11/2026 (Run 14, the spread of the crew): the plan indices of the current <b>window</b> (the piece the walk is on
	/// plus the next ones of the same run, at most <see cref="BuildSpread.Window"/>), and their poses - ONE list each,
	/// reused (the window is built per piece, never per frame, and a plan has 1000 of them).
	/// </summary>
	private static readonly List<int> s_spreadWindow = new List<int>(BuildSpread.Window);

	/// <summary>11/11/2026 (Run 14, the spread of the crew): the poses of <see cref="s_spreadWindow"/>, in the same order
	/// (the candidates of <see cref="BuildSpread.Pick"/>).</summary>
	private static readonly List<Vector3> s_spreadPoses = new List<Vector3>(BuildSpread.Window);

	/// <summary>11/11/2026 (Run 14, the spread of the crew): the poses of the pieces the other workers of the site are on
	/// (<see cref="MHR_BuildGroup.ClaimPoses"/>) - what a candidate keeps away from. ONE list reused.</summary>
	private static readonly List<Vector3> s_spreadClaims = new List<Vector3>();

	/// <summary>P1 (04/11/2026, measured with the deck run of the 985-piece house: 16 pieces - floors and roofs of the east
	/// gable and of the north-west corner, 40,8 to 46,8 - were left out with <c>no_support</c>): the author's blueprint has
	/// <b>no support piece under them</b> (his house leaned on something the file does not carry), so the direct-support pull
	/// of the plan answers -1 and the piece can never stand. These are the posts planted under such a piece, <b>shortest
	/// first</b>: the smallest one that reaches from the surface below to the piece, so the base stands on the ground/floor and
	/// the top touches the piece (measured in the run: planting a 2 m post under a piece 1 m above the ground buried it 1 m and
	/// the game refused the POST).</summary>
	private static readonly string[] FallbackPostPrefabs = { "wood_beam_1", "wood_beam", "wood_pole2", "wood_beam_45" };

	/// <summary>P1 (04/11/2026): the length of each <see cref="FallbackPostPrefabs"/> entry, measured from the prefab's own
	/// snap points on first use (they do not change; 0 = the prefab is not in this game, skipped). Measured this way and not
	/// hard-coded because the repo only had <c>wood_pole2</c> confirmed (2,00 m, <c>docs/re/building-notes.md</c> §11) - and a
	/// wrong length is exactly what broke the first version.</summary>
	private static readonly float[] s_postLengths = new float[FallbackPostPrefabs.Length];

	private static bool s_postsMeasured;

	/// <summary>P1 (04/11/2026): how far under the piece the surface a post stands on is looked for. The deepest gap the
	/// measured house has is 7 m (the top of the gable); 10 m is the vanilla placement ray's reach and leaves room. It is the
	/// TOTAL of the walk through the close surfaces, not the reach of each pass.</summary>
	private const float PostReach = 10f;

	/// <summary>P1 (04/11/2026, measured: a roof eave 0,16 m under a floor of 40,8 stopped the ray and no post fits in
	/// 16 cm): how deep under the piece a surface has to be to serve as the base of a post. Anything closer is walked
	/// through (a roof eave, a beam lying under the piece); the deck floor 1,02 m down is the base the post wants.</summary>
	private const float MinPostGap = 0.4f;

	/// <summary>P1 (04/11/2026): how many surfaces (<see cref="MinPostGap"/>) the ray may walk through before the terrain
	/// height is used. Four is slack: a roof eave, a beam and a rafter are three.</summary>
	private const int MaxPostPasses = 4;

	/// <summary>P1 (04/11/2026): the depths (piece minus hit, in metres) of the surfaces the post ray met, in the order it
	/// met them - a scratch buffer, no allocation per refusal.</summary>
	private static readonly float[] s_postDepths = new float[MaxPostPasses];

	/// <summary>P1 (11/11/2026): whether each of those surfaces can <b>hold</b> the post (<see cref="HoldsPlacedPiece"/> with
	/// the class of a beam/pole). The ray walks through the ones that do not (a roof eave 2,8 m to the side of the piece, the
	/// top of a chest) - the depth alone planted the post over nothing and the game refused the post itself with
	/// <c>no_support</c> 95 times.</summary>
	private static readonly bool[] s_postHolds = new bool[MaxPostPasses];

	/// <summary>P1 (04/11/2026): how many emergency posts one ORDER may add - the house gets "a few extra posts", never a
	/// forest of them. One per piece (<c>State.FallbackPosts</c>), never where the plan already has a support.</summary>
	internal const int MaxFallbackPosts = 24;

	/// <summary>E10: how many leveling passes a job may apply before it gives up (each moves the ground by at most 1 m).</summary>
	internal const int MaxLevelPasses = 4;
	/// <summary>E10: above this height variation (metres) the soft level cannot win - the spot is refused instead of digging a crater.</summary>
	internal const float MaxLevelVariation = 1.8f;

	/// <summary>E4b: how long a pending build waits for the gather <b>without any progress</b> before it is dropped. Any
	/// wood that arrives resets it (see <see cref="Pending.LastProgressAt"/>).</summary>
	internal const float GatherTimeout = 300f;

	/// <summary>P1 (04/11/2026, the stall watchdog): how long the site may go <b>without a piece going up</b> before the
	/// pieces still waiting in the retry queue are given up and the order ends. It replaces the old "60 s in the wait"
	/// clock, which said nothing about the site moving (a worker could give up on the queue while another one was placing
	/// pieces).</summary>
	internal const float StallSeconds = 60f;

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
	/// <summary>E7: most workbenches one order adds. P1 (04/11/2026): 8 was not enough for a 32 m house - the pass stopped
	/// at 6 SEARCHES and the middle of the plan (measured on the 985-piece house) had no station in range.</summary>
	private const int MaxPlannedBenches = 12;
	/// <summary>E7: most spot searches for those benches - every search walks the placement ghost over a few poses around
	/// one anchor (see <see cref="FindBenchSpotOutside"/>: ~36 poses per anchor, up to 4 anchors) and a site with nothing
	/// free must not freeze the frame. It was 6 and the measured house ran out of it after 2 benches.</summary>
	private const int MaxBenchSearches = 32;
	/// <summary>The workbench's coverage in metres when the prefab cannot be read: <c>CraftingStation.m_rangeBuild</c> is 20
	/// (the radius in which a station frees building - measured, see <c>docs/re/building-notes.md</c>).</summary>
	private const float BenchDefaultRadius = 20f;

	/// <summary>P1 (04/11/2026): how high the whole plan of a blueprint rises when <c>[Building] DeckLift</c> is not bound
	/// (the config's own default: 1 m - the ground floor sits on a low deck over the poles).</summary>
	private const float DeckLiftDefault = 1f;

	/// <summary>P1 (04/11/2026): the pose budget of the <b>first</b> nudge search (2 rings x 8 directions = 17 spots, up to
	/// 4 yaws each, capped at 48 checks - the number of 03/11/2026). <see cref="BuildSite.Find"/> counts CHECKS and not
	/// spots, and a piece the game lets rotate is tried with the 4 yaws before the spot is abandoned.</summary>
	private const int NudgeChecks = 48;

	/// <summary>P1 (04/11/2026): the wide search of the <b>furniture</b> nudge - 3 rings x 12 directions out to the 5 m
	/// radius (37 spots with the hint) x 4 yaws. The cap is exactly the spots' worth of checks: a bigger radius with the
	/// old 48 would spend the whole budget on the first ring and never reach the 5 m.</summary>
	private const int NudgeChecksWide = 37 * 4;

	/// <summary>P1 (04/11/2026): how far a <b>furniture</b> piece may be moved from the plan's pose - 5 m, the radius the
	/// two chests of the measured run needed (2,5 m found no valid pose on the bank at y 39,9). A <b>structural</b> piece
	/// keeps the 1 m of 03/11/2026: a wall or a roof row metres off the plan is not the same house.</summary>
	private const float NudgeFurnitureRadius = 5f;

	/// <summary>P1 (04/11/2026): the small radius of a structural piece (<see cref="NudgeFurnitureRadius"/>).</summary>
	private const float NudgeStructuralRadius = 1f;

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
		/// <summary>P1 (multi-build, 03/11/2026): a <b>follower</b> waits while the leader levels and clears the site. The
		/// phase does nothing at all (no mover, no timeout, no cancellation) until
		/// <see cref="MHR_BuildGroup.BuildPhase"/> opens - the order is alive, so the idle job does not take the body.</summary>
		WaitGroup,
		/// <summary>P1 (review A1, 03/11/2026): the worker finished the plan and waits for the pieces of the
		/// <b>group's</b> retry queue (they may be another worker's, or not due yet) instead of ending the order.</summary>
		WaitRetry,
		Find,
		Goto,
		Place,
		/// <summary>
		/// 11/11/2026 (the body barrier): the pose was refused because a BODY (the player walking) was on it - the piece is
		/// kept (same index, same claim, nothing paid) and the same pose is tried again after
		/// <see cref="BuildFailure.ShortRetryDelay"/>.
		/// </summary>
		BodyRetry,
		/// <summary>E4b: walking to the resource BEFORE the gather mode is set (only the build steers here).</summary>
		GatherWalk,
		Verify,
		Done
	}

	private sealed class State
	{
		internal bool Active;
		/// <summary>
		/// 11/11/2026 (the build panel): the order is <b>paused</b> by the player - <see cref="MHR_Build.Drive"/> answers
		/// "still running" and does nothing, the plan/claims/counters stay exactly where they are. It is not a "para":
		/// <see cref="MHR_Build.Resume"/> continues at the same piece.
		/// </summary>
		internal bool Paused;
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
		/// <summary>P1 (multi-build, 03/11/2026): the <b>group</b> this worker belongs to (the key of
		/// <see cref="MHR_BuildGroup"/>; null for a normal single-worker order). The plan is the group's, the pieces are
		/// claimed per index and the site work (level/clear) is the leader's - <see cref="MHR_BuildGroup.IsLeader"/>
		/// answers who the leader is NOW (it is reassigned when the old one leaves).</summary>
		internal string Group;
		/// <summary>E7: [Building] BlueprintFree - do not pay for the pieces (a test/creative switch).</summary>
		internal bool Free;
		/// <summary>P1 (04/11/2026, "contadores honestos"): the plan indices of the pieces that went up and that were given
		/// up - SETS, so a piece that failed four times is one skipped piece, not four (the run of 03/11/2026 said "262
		/// skipped" with a plan of 984). They are disjoint: a piece that is given up and then goes up is placed, never both.
		/// The group keeps the same sets for the site; these are the single-worker path's own numbers (and the fallback when
		/// the group is gone).</summary>
		internal readonly HashSet<int> PlacedIndices = new HashSet<int>();
		internal readonly HashSet<int> SkippedIndices = new HashSet<int>();
		/// <summary>P1 (04/11/2026, incident): the plan indices whose direct support was already pulled for this worker.
		/// Pulling it again for the same piece loops forever (refused -> support -> refused; the log of the 290afc1 run
		/// had 1.4M lines and the game froze), so it is once per piece, with a small cap per order.</summary>
		internal readonly HashSet<int> PulledSupport = new HashSet<int>();
		/// <summary>P1 (04/11/2026): the plan indices whose pose was already adjusted ONCE after a fall (a piece the
		/// companion fell from twice, see <c>MHR_Build.AdjustFallenPose</c>) - one adjustment per piece, like the support
		/// pull: repeating it every two falls would walk him into the same hole again.</summary>
		internal readonly HashSet<int> FallAdjusted = new HashSet<int>();
		/// <summary>P1 (04/11/2026): the plan indices that already got an <b>emergency post</b> under them (a wooden post
		/// planted because the plan has nothing under the piece - <c>MHR_Build.TryFallbackPost</c>). One per piece, and the
		/// size of the set is the order's budget (<c>MaxFallbackPosts</c>).</summary>
		internal readonly HashSet<int> FallbackPosts = new HashSet<int>();
		/// <summary>
		/// E7 (a ideia do "Build all" deles sem o BuildOrders, 03/11/2026): a piece that does not stand YET (no support: its
		/// floor is not up, its wall is missing) goes to this queue instead of being lost. When the plan ends it is tried
		/// again - the second pass is what makes a house converge (floors, then walls, then roof).
		/// </summary>
		internal List<PlannedStep> Retry;
		/// <summary>P1 (04/11/2026): the single worker's own retry clock - the pieces that failed by <b>dependency</b> are not
		/// walked to again before it (the group has the same wait per entry, see <c>RetryEntry.At</c>).</summary>
		internal float RetryAt;
		/// <summary>P1 (04/11/2026): when this worker last saw a piece go up - the stall watchdog's clock for a single
		/// worker (the group has its own, on the site).</summary>
		internal float LastProgressAt;
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
		/// <summary>E7 (review, 03/11/2026): the current piece was already offered to the nearest-floor reorder (once per piece).</summary>
		internal bool FloorPreferDone;
		/// <summary>E7 (review, 03/11/2026): last plan index brought forward by PullSupports/PullFloorsAbove; the floor reorder must not touch it.</summary>
		internal int PullUntil = -1;
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
		/// <summary>
		/// 11/11/2026 (Run 13, the ridge of the <c>CommHomeBasicBP</c>): the surface the walk is heading for instead of the
		/// piece in mid-air (<see cref="MHR_Build.WalkSpot"/>, <see cref="BuildWalkSpot.Choose"/>), and the plan index it was
		/// chosen for. The spot is kept while the piece does not change (<see cref="WalkFor"/>): the rays are not cast on
		/// every frame of the walk, and a spot of the previous piece is never reused.
		/// </summary>
		internal Vector3 WalkTo;
		internal bool HasWalkTo;
		internal int WalkFor = -1;
		/// <summary>E7 (player's idea, 03/11/2026): the temporary stairs placed to reach a piece above the eye rule
		/// (<see cref="MaxPlace"/> - vanilla 5 m, <c>[Building] PlaceReach</c>). They are NOT part of the plan and are
		/// removed when the order ends.</summary>
		internal readonly List<GameObject> Scaffolds = new List<GameObject>();
		/// <summary>E7: how many temporary stairs this ORDER already placed, and the cooldown before the next attempt
		/// (the budget is per order, never per piece - 985 pieces x 3 covered the site in stairs).</summary>
		internal int ScaffoldTries;
		internal float NextScaffoldAt;
		/// <summary>E7 (review, 03/11/2026): the pieces accepted while a temporary stair stood - they are asked again for
		/// support before the stair is removed, so a roof that was held up by it does not get a hole.</summary>
		internal readonly List<GameObject> Scaffolded = new List<GameObject>();
		/// <summary>11/11/2026 (the body barrier, single worker): how many short retries the CURRENT piece already had for a
		/// body (the same 3 of the group, reset when the piece changes - a group counts them on the site).</summary>
		internal int BodyTries;
		/// <summary>
		/// 11/11/2026 (the <b>storey barrier</b> of the Run 9, the player's rule: "they may skip, but come back and try again
		/// before going up"): the height of the piece the walk was about to take when the barrier started - 0 when no barrier
		/// is drawn. While it is set, <see cref="MHR_Build.Advance"/> hands back the pieces of the queue that are
		/// <b>below</b> it (<see cref="MHR_Build.StoreyBarrier"/>) before taking anything of the storey above.
		/// </summary>
		internal float BarrierNextY;
		/// <summary>The plan index that piece had (the walk resumes there after the barrier is served).</summary>
		internal int BarrierNextIndex = -1;
		/// <summary>When the barrier started - it is what bounds it (<see cref="BuildFailure.BodyRetryMaxWait"/>).</summary>
		internal float BarrierStart;
		/// <summary>The plan indices this barrier already gave a turn to: a piece is served once per barrier (it may be
		/// refused again by a body, and the next barrier - after a reset - gives it another turn).</summary>
		internal readonly HashSet<int> BarrierServed = new HashSet<int>();
		/// <summary>
		/// 11/11/2026 (the storey barrier): the plan index the walk goes back to once the piece the barrier served is done
		/// (-1 = none). <see cref="MHR_Build.Advance"/> restores it as <c>Index - 1</c> so the walk continues at the piece it
		/// was about to take, <b>without</b> re-walking everything in between.
		/// </summary>
		internal int ResumeIndex = -1;
		/// <summary>11/11/2026 (Run 17): seconds this order spent with the barrier holding the plan for a <b>body</b>.</summary>
		internal float WaitBody;
		/// <summary>11/11/2026 (Run 17): seconds spent with the barrier holding the plan for a <b>dependency</b>.</summary>
		internal float WaitDependency;
		/// <summary>
		/// 11/11/2026 (Run 17, the wait instrumented; Run 18 split it in two): seconds spent with the site's queue holding
		/// pieces this worker could not take, at the <b>end</b> of the plan - nothing ahead of the walk, so the queue is
		/// the legitimate way forward.
		/// </summary>
		internal float WaitQueueEnd;
		/// <summary>
		/// 11/11/2026 (Run 18, the starvation of the wait): the same wait in the <b>middle</b> of the plan, where there is
		/// still a piece ahead of the walk. With the <c>ahead</c> guard of <see cref="MHR_Build.WaitRetry"/> this is ≈ 0 by
		/// construction (the frame goes back to <see cref="MHR_Build.Advance"/> instead of sitting in the queue): a number
		/// that grows here means the guard stopped catching the case it was written for.
		/// </summary>
		internal float WaitAhead;
		/// <summary>
		/// 11/11/2026 (Run 18): seconds the site's <b>gate</b> itself held this worker (<c>MHR_BuildGroup.MayTake</c> false
		/// inside <see cref="MHR_Build.Advance"/>). Until now that time fell in <b>no</b> bucket at all: the wait called
		/// Advance with <c>dt = 0</c>, so the gate was invisible in the summary line.
		/// </summary>
		internal float WaitGate;
		/// <summary>11/11/2026 (Run 17): seconds spent walking the step aside of a piece another companion places.</summary>
		internal float WaitAside;
		/// <summary>11/11/2026 (Run 17): how many times the site's gate kept this worker from taking a piece (a body
		/// holding something below it).</summary>
		internal int BarrierHolds;
		/// <summary>11/11/2026 (Run 17): how many times the barrier itself held this worker waiting in the queue.</summary>
		internal int BodyHolds;
		/// <summary>11/11/2026 (Run 10): the body on the current piece was already named in the log (the capsule numbers are
		/// a measurement, not a watch - the refusal repeats every frame of a short retry).</summary>
		internal bool BodyLogged;
		/// <summary>11/11/2026 (Run 10, the companion blocker): the "step aside" of the current piece was already said in the
		/// log (the request itself repeats, and is deduplicated by the site).</summary>
		internal bool AsideLogged;
		/// <summary>
		/// 11/11/2026 (Run 10, the companion blocker): where this worker is walking to in order to leave a piece another one
		/// is placing (<see cref="MHR_BuildGroup.TakeStepAside"/>) - <see cref="Vector3.zero"/> when it has nowhere to go.
		/// </summary>
		internal Vector3 AsideTarget;
		/// <summary>When the step-aside walk is over (it is a step, not a walk across the site).</summary>
		internal float AsideUntil;
		/// <summary>
		/// 11/11/2026 (Run 14): what the surface search of the current piece cost - how many rays were cast and how many
		/// surfaces came back (<see cref="MHR_Build.WalkSpot"/>). It is what tells the tester <b>where it looked</b> when the
		/// chosen spot is the floor of the storey below (the Run 14 ridge: walk spot 41,9 for a piece at 48,8).
		/// </summary>
		internal int WalkRays;
		/// <summary>11/11/2026 (Run 14): the surfaces that search found (see <see cref="WalkRays"/>).</summary>
		internal int WalkFound;
		/// <summary>
		/// 11/11/2026 (Run 14, the spread of the crew): how many pieces of the plan this worker <b>left for later</b> to take
		/// one that was far from what the others were building (<see cref="MHR_Build.SpreadWindow"/>), how many of those it
		/// took back from the site's queue, and how many <b>step-aside</b> asks it made. They are the three numbers of the
		/// one summary line of <see cref="MHR_Build.Finish"/> (the Run 14 log had to be grepped line by line).
		/// </summary>
		internal int SpreadSkipped;
		/// <summary>11/11/2026 (Run 14): how many pieces left for later this worker pulled back from the site's queue as a
		/// spread entry (see <see cref="SpreadSkipped"/>).</summary>
		internal int SpreadResumed;
		/// <summary>11/11/2026 (Run 14): how many step-aside asks this order really made (a request that the site answered
		/// <c>Asked</c> - the dedup and the anti ping-pong of a pair are not asks).</summary>
		internal int AsideAsked;
	}

	/// <summary>E6: one piece of a plan, already resolved to a world pose.</summary>
	/// <remarks>P1 (multi-build): <c>internal</c> because the <b>group</b> owns the list - every worker of a site walks
	/// the same steps by the same index (<see cref="MHR_BuildGroup"/>).</remarks>
	internal sealed class PlannedStep
	{
		internal string Prefab;
		internal Piece Piece;
		internal Vector3 Pos;
		internal Quaternion Rot;
		/// <summary>
		/// 11/11/2026 (the body barrier): the <b>storey</b> of the piece - the key the blueprint's order uses
		/// (<c>BuildBlueprintParser.Line.Level</c>: its own height, or the slab a roof covers). The site holds back only the
		/// pieces above the storey a body is standing on (<see cref="MHR_BuildGroup.MayTake"/>). 0 for a template (it has no
		/// storeys: nothing is ever held back).
		/// </summary>
		internal float Level;
		/// <summary>P1 (04/11/2026, the support gate): how many times this piece failed for a <b>real</b> reason while it
		/// waits in the single worker's retry list (the cap is <see cref="MaxPasses"/>). A failure by dependency does not
		/// come here.</summary>
		internal int Tries;
		/// <summary>P1 (04/11/2026): how many times this piece was deferred because what holds it up was not there yet -
		/// the wait before looking at it again grows with this number.</summary>
		internal int Dependency;
		/// <summary>11/11/2026 (the storey barrier): how many times this piece was refused because a <b>body</b> was on the
		/// spot - it never spends an attempt, so it is counted apart from <see cref="Tries"/>.</summary>
		internal int Blocked;
	}

	private static readonly Dictionary<int, State> s_state = new Dictionary<int, State>();
	/// <summary>E7: cached layer mask of the scaffold probe (-1 = not resolved yet).</summary>
	private static int s_scaffoldMask = -1;

	/// <summary>E7: when a blueprint last asked for the ground to be leveled (a guard against the measure/level loop).</summary>
	private static readonly Dictionary<string, float> s_levelTried = new Dictionary<string, float>();

	/// <summary>
	/// P1 (review A5, 03/11/2026): the level job just worked this blueprint's site - the resumed order must not ask for
	/// another leveling right away. The guard above is 90 s and a big site takes longer than that to level (the player's
	/// measured 4 passes of ~24 s), so a resume would level it again and again instead of building.
	/// </summary>
	internal static void NoteLeveled(string blueprintFile)
	{
		if (!string.IsNullOrEmpty(blueprintFile))
		{
			s_levelTried[blueprintFile] = Time.time;
		}
	}

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
		/// <summary>P1 (multi-build, 03/11/2026): the group of the site, when the order was a member of one - the resumed
		/// order goes back to it (if it is still alive) and claims a piece again.</summary>
		internal string Group;
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
		/// <summary>
		/// 11/11/2026 (the build panel): the order waiting for its materials is <b>paused</b> by the player - the watcher
		/// does not touch it (no resume when the bag is covered, no gather timeout) until
		/// <see cref="MHR_Build.Resume"/> continues it.
		/// </summary>
		internal bool Paused;
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
	/// skipped when the attempts are used up. This is the "build what stands, come back for the rest" loop of their
	/// BuildOrders, done with the game's own verdict instead of a stability simulation.
	///
	/// <para>P1 (review A1, 03/11/2026): in a <b>group</b> the queue is the SITE's, not the worker's - the piece was
	/// skipped by the follower and died with it (the group's counters never saw it). The index goes to
	/// <see cref="MHR_BuildGroup.TryQueueRetry"/> and any worker pulls it again.</para>
	///
	/// <para>P1 (04/11/2026, the <b>support gate</b>): a piece that fails because what holds it up is not there yet is a
	/// <b>dependency</b> - it spends no attempt, is never counted as skipped and comes back with a growing wait (the plan
	/// is still going to put its support up). Everything else is a real failure and keeps the 3-attempt cap. The
	/// classification is in <see cref="BuildFailure.Classify"/>.</para>
	///
	/// <para>11/11/2026 (the <b>storey barrier</b>): a piece refused because a <b>body</b> was on the spot
	/// (<see cref="BuildFailure.Kind.Blocked"/>) spends no attempt either - the pose is fine, the player is walking - and
	/// comes back after the short <see cref="BuildFailure.BlockedDelay"/>. The plan does not climb over it: the barrier
	/// hands it back before the next storey.</para>
	/// </summary>
	private static void QueueRetry(CompanionAI ai, State state, string why)
	{
		BuildFailure.Kind kind = ClassifyFailure(state, why);
		if (state.Group != null && ai != null)
		{
			// the piece is free again for the others right away (one of them may have the support/tool that was missing)
			MHR_BuildGroup.Release(state.Group, state.Index, ai.GetInstanceID());
			if (MHR_BuildGroup.TryQueueRetry(state.Group, state.Index, kind))
			{
				if (kind == BuildFailure.Kind.Blocked)
				{
					// 11/11/2026 (Run 17): the wait it really got (it grows with the refusals) - the line said 2 s always
					float wait = BuildFailure.BlockedDelay(MHR_BuildGroup.BlockedOf(state.Group, state.Index));
					Log(ai, "corpo no caminho: " + state.Prefab + " em (" + F(state.Pos.x) + ", " + F(state.Pos.y) + ", "
						+ F(state.Pos.z) + ") - volta em " + wait.ToString("F0")
						+ " s (não gasta tentativa)");
					return;
				}
				Log(ai, (kind == BuildFailure.Kind.Dependency ? "sem apoio ainda: " : "retry no grupo: ") + state.Prefab
					+ " (" + why + ") em (" + F(state.Pos.x) + ", " + F(state.Pos.y) + ", " + F(state.Pos.z) + ")"
					+ (kind == BuildFailure.Kind.Dependency ? " - não gasta tentativa, volta com espera" : " - a peça volta para a fila do canteiro"));
				return;
			}
			SkipPiece(ai, state, why);
			return;
		}
		if (state.Retry != null && state.Piece != null)
		{
			// the step the worker is on: reusing the plan's own object keeps the piece's Tries/Dependency across the retry
			// passes (a fresh copy would reset them and the piece would never be given up)
			PlannedStep pending = null;
			if (state.Plan != null && state.Index >= 0 && state.Index < state.Plan.Count)
			{
				PlannedStep current = state.Plan[state.Index];
				if (SamePiece(current, state))
				{
					pending = current;
				}
			}
			if (pending == null)
			{
				pending = new PlannedStep { Prefab = state.Prefab, Piece = state.Piece, Pos = state.Pos, Rot = state.Rot };
			}
			if (state.Retry.IndexOf(pending) < 0)
			{
				state.Retry.Add(pending);
			}
			if (kind == BuildFailure.Kind.Dependency)
			{
				pending.Dependency++;
				state.RetryAt = Time.time + DependencyDelay(pending.Dependency);
				Log(ai, "sem apoio ainda: " + state.Prefab + " (" + why + ") em (" + F(state.Pos.x) + ", " + F(state.Pos.y)
					+ ", " + F(state.Pos.z) + ") - não gasta tentativa, volta com espera");
				return;
			}
			if (kind == BuildFailure.Kind.Blocked)
			{
				pending.Blocked++;
				float wait = BuildFailure.BlockedDelay(pending.Blocked);
				state.RetryAt = Time.time + wait;
				Log(ai, "corpo no caminho: " + state.Prefab + " em (" + F(state.Pos.x) + ", " + F(state.Pos.y) + ", "
					+ F(state.Pos.z) + ") - volta em " + wait.ToString("F0") + " s (não gasta tentativa)");
				return;
			}
			pending.Tries++;
			if (pending.Tries > MaxPasses)
			{
				state.Retry.Remove(pending);
				SkipPiece(ai, state, why);
				return;
			}
			state.RetryAt = Time.time + MHR_BuildGroup.RetryDelay;
			Log(ai, "retry later: " + state.Prefab + " (" + why + ") at (" + F(state.Pos.x) + ", " + F(state.Pos.y) + ", "
				+ F(state.Pos.z) + ") - " + pending.Tries + "/" + MaxPasses);
			return;
		}
		SkipPiece(ai, state, why);
	}

	/// <summary>
	/// P1 (04/11/2026, the <b>support gate</b>): the classification itself lives in <see cref="BuildFailure"/> - it is pure
	/// (string → class) and the harness asserts the whole table (section (p)). Here only the world is added: the placement
	/// probe's answer ("is there anything under the pose?") feeds <c>groundBelow</c>, which <see cref="BuildFailure"/> reads
	/// for <c>no_ground</c> only.
	/// </summary>
	private static BuildFailure.Kind ClassifyFailure(State state, string why)
	{
		if (state == null)
		{
			return BuildFailure.Kind.Real;
		}
		bool groundBelow = string.Equals(why, "no_ground", StringComparison.OrdinalIgnoreCase) && GroundUnder(state.Pos);
		return BuildFailure.Classify(why, groundBelow);
	}

	/// <summary>
	/// P1 (04/11/2026): is there <b>anything</b> under this pose for the placement probe? It mirrors
	/// <c>BuildProbe.CheckPoint</c> (a ray from 4 m above over 8 m down, on the placement layers): a <c>no_ground</c> that
	/// hits nothing is the spot failing, while one that hits a piece of the plan is the support that is not up yet.
	/// </summary>
	private static bool GroundUnder(Vector3 pos)
	{
		return Physics.Raycast(pos + Vector3.up * 4f, Vector3.down, 12f, BuildProbe.PlaceMask(),
			QueryTriggerInteraction.Ignore);
	}

	/// <summary>P1 (04/11/2026): how long a piece deferred by <b>dependency</b> waits before it is looked at again - what it
	/// needs may be going up right now. It grows and stops at 25 s.</summary>
	internal static float DependencyDelay(int deferrals)
	{
		if (deferrals <= 1)
		{
			return 8f;
		}
		if (deferrals == 2)
		{
			return 12f;
		}
		if (deferrals == 3)
		{
			return 20f;
		}
		return 25f;
	}

	/// <summary>Is the step of the plan the piece this worker is on (prefab and pose)? It is how the single worker's retry
	/// list finds the same piece again instead of growing a copy per pass.</summary>
	private static bool SamePiece(PlannedStep step, State state)
	{
		return step != null && state != null && step.Prefab == state.Prefab
			&& (step.Pos - state.Pos).sqrMagnitude < 0.0001f;
	}

	/// <summary>
	/// P1 (04/11/2026, "contadores honestos"): the plan index a piece is counted by. For a site it is the plan's index
	/// (a group never appends to the plan). For a single worker the retry pass <b>appends the same step again</b>, so the
	/// copy at the end is not another piece: the FIRST place the step appears is the piece's identity.
	/// </summary>
	private static int PieceIndex(State state)
	{
		if (state == null)
		{
			return -1;
		}
		if (state.Group != null || state.Plan == null || state.Index < 0 || state.Index >= state.Plan.Count)
		{
			return state.Index;
		}
		PlannedStep step = state.Plan[state.Index];
		int first = step != null ? state.Plan.IndexOf(step) : -1;
		return first >= 0 ? first : state.Index;
	}

	/// <summary>
	/// P1 (04/11/2026, "contadores honestos"): one piece is given up - counted <b>once per plan index</b>, in the order and
	/// in the site. Every path that abandons a piece goes through here (the retry cap, the stall watchdog, a fall and the
	/// timeout), which is what makes <c>Placed + Skipped</c> close with the plan instead of counting attempts (the run of
	/// 03/11/2026 said 262 skipped with 984 pieces).
	/// </summary>
	private static void SkipPiece(CompanionAI ai, State state, string why)
	{
		if (state == null)
		{
			return;
		}
		int index = PieceIndex(state);
		if (index < 0 || state.PlacedIndices.Contains(index))
		{
			return;   // a piece that is up is never counted as skipped
		}
		state.SkippedIndices.Add(index);
		MHR_BuildGroup.AddSkipped(state.Group, index);
		int total = state.Group != null ? MHR_BuildGroup.Skipped(state.Group) : state.SkippedIndices.Count;
		Log(ai, "desistindo de " + state.Prefab + " (" + why + ") em (" + F(state.Pos.x) + ", " + F(state.Pos.y) + ", "
			+ F(state.Pos.z) + ") - " + total + " pulada(s)");
	}

	/// <summary>
	/// E7 (test of the 985-piece house, 03/11/2026): the blueprint order is a snake that starts where the previous run
	/// ended, not where the companion is standing. After a fall, a death or a recover he can be 10-17 m from the next
	/// floor and the mover cannot cross the floor that is not built yet ("no progress ... 12 s"). For FLOORS of the same
	/// height, take the nearest one to where he is standing now: the floor is filled around him instead of from the far
	/// end of the snake. Only the order changes; no piece is added or removed. Review (03/11/2026): the pieces brought
	/// forward by <see cref="PullSupports"/>/<see cref="PullFloorsAbove"/> keep their place (<c>PullUntil</c>), and a
	/// floor he can place without walking wins over a nearer-looking one that still needs a walk. Both windows follow
	/// <see cref="MaxPlace"/> (the configurable reach): the "close enough" one is <c>MaxPlace + 1</c> and the second pass
	/// looks up to <c>MaxPlace + 2</c>.
	/// </summary>
	private static bool PreferNearFloor(CompanionAI ai, State state, int index)
	{
		if (PlanLocked(state) || state.Plan == null || index < 0 || index + 1 >= state.Plan.Count)
		{
			return false;
		}
		// a piece brought forward by PullSupports/PullFloorsAbove keeps its place: it is what holds the refused piece up
		if (index <= state.PullUntil)
		{
			return false;
		}
		PlannedStep next = state.Plan[index];
		if (!IsFloorStep(next))
		{
			return false;
		}
		Vector3 me = ai.transform.position;
		// a floor one storey above/below needs the ladder, not a reorder (a storey is 2 m)
		if (Mathf.Abs(me.y - next.Pos.y) > 1.5f)
		{
			return false;
		}
		float current = FlatDistance(me, next.Pos);
		if (current <= MaxPlace + 1f)
		{
			return false;
		}
		// first any floor he can place WITHOUT WALKING (the normal Reach + 0,5 = 3 m and the eye rule); only then the
		// nearest up to 7 m, which he still has to walk to
		int best = -1;
		float bestDist = float.MaxValue;
		Vector3 eye = new Vector3(me.x, me.y + EyeHeight, me.z);
		for (int pass = 0; pass < 2 && best < 0; pass++)
		{
			float limit = pass == 0 ? Reach + 0.5f : MaxPlace + 2f;
			for (int i = index + 1; i < state.Plan.Count; i++)
			{
				PlannedStep step = state.Plan[i];
				if (!IsFloorStep(step) || Mathf.Abs(step.Pos.y - next.Pos.y) > 0.75f)
				{
					continue;
				}
				if (pass == 0 && Vector3.Distance(eye, step.Pos) > MaxPlace)
				{
					continue;
				}
				float d = FlatDistance(me, step.Pos);
				if (d <= limit && d < bestDist)
				{
					bestDist = d;
					best = i;
				}
			}
		}
		if (best < 0 || bestDist >= current - 1f)
		{
			return false;
		}
		PlannedStep pick = state.Plan[best];
		state.Plan[best] = next;
		state.Plan[index] = pick;
		LogDebug(ai, "floor order: (" + F(pick.Pos.x) + ", " + F(pick.Pos.z) + ") is " + F(bestDist)
			+ " m from the companion - before the one at " + F(current) + " m");
		return true;
	}

	private static bool IsFloorStep(PlannedStep step)
	{
		if (step == null || string.IsNullOrEmpty(step.Prefab))
		{
			return false;
		}
		// ClassOf also gives 0 to the workbench - the station is not a floor (review, 03/11/2026)
		if (step.Prefab.IndexOf("workbench", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return false;
		}
		return step.Prefab.IndexOf("floor", StringComparison.OrdinalIgnoreCase) >= 0
			|| step.Prefab.IndexOf("foundation", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static float FlatDistance(Vector3 a, Vector3 b)
	{
		float dx = a.x - b.x;
		float dz = a.z - b.z;
		return Mathf.Sqrt(dx * dx + dz * dz);
	}

	/// <summary>
	/// 11/11/2026 (Run 14): the <b>fast path</b> of the spread - only a <b>blueprint</b> plan shared by <b>more than one</b>
	/// worker spreads (a template and a single piece have one worker each, and the switch may be off). False keeps the walk
	/// byte for byte as it was.
	/// </summary>
	private static bool SpreadOn(State state)
	{
		return state != null && state.Plan != null && state.Blueprint != null && state.Group != null && SpreadWorkers
			&& MHR_BuildGroup.IsShared(state.Group);
	}

	/// <summary>
	/// 11/11/2026 (Run 14, the <b>spread of the crew</b>, the player's report <i>"eles parecem estar disputando pra ver quem
	/// coloca a peça primeiro, em vez de trabalhar em equipe"</i>): the pieces of the plan this worker may take instead of
	/// <paramref name="next"/> - the next one plus the ones right after it, at most <see cref="BuildSpread.Window"/>, and
	/// only while they are the <b>same run</b> (<see cref="BuildSpread.SameRun"/>: same storey key, same class, same
	/// height, which is exactly the block <c>BuildBlueprintParser.NearestWithin</c> reorders). The plan's consecutive
	/// indices are neighbours in space, so five companions on the same plan reach for the same corner at the same time -
	/// the Run 14 log measured 88 <c>corpo na peça</c> and 32 step-aside asks.
	///
	/// <para>A piece another worker already holds or that the site already built is <b>not a candidate</b> (it is not work
	/// looking for a body); the first piece of another run <b>ends</b> the window - the plan's own order says where the row
	/// ends. Fills <paramref name="indices"/> and <paramref name="poses"/> in the same order (the first one is always
	/// <paramref name="next"/>) and answers how many there are: 0 or 1 means there is nothing to spread with and the walk
	/// takes <paramref name="next"/>, exactly as before.</para>
	/// </summary>
	private static int SpreadWindow(State state, int next, List<int> indices, List<Vector3> poses)
	{
		if (state == null || state.Plan == null || indices == null || poses == null || next < 0 || next >= state.Plan.Count)
		{
			return 0;
		}
		PlannedStep first = state.Plan[next];
		if (first == null)
		{
			return 0;
		}
		indices.Clear();
		poses.Clear();
		int firstClass = BuildBlueprintParser.ClassOf(first.Prefab);
		indices.Add(next);
		poses.Add(first.Pos);
		for (int i = next + 1; i < state.Plan.Count && indices.Count < BuildSpread.Window; i++)
		{
			PlannedStep step = state.Plan[i];
			if (step == null || string.IsNullOrEmpty(step.Prefab))
			{
				continue;
			}
			if (!BuildSpread.Finite(step.Pos))
			{
				continue;   // a pose that is not a number is never a destination (and never enters the site's queue)
			}
			if (!BuildSpread.SameRun(firstClass, first.Level, first.Pos.y,
				BuildBlueprintParser.ClassOf(step.Prefab), step.Level, step.Pos.y))
			{
				break;   // another run of the plan: the window is this row, never the whole storey
			}
			if (state.Group != null && (MHR_BuildGroup.IsDone(state.Group, i) || MHR_BuildGroup.OwnerOf(state.Group, i) != 0))
			{
				continue;   // already up, or another companion is on it
			}
			indices.Add(i);
			poses.Add(step.Pos);
		}
		return indices.Count;
	}

	/// <summary>
	/// E6/E7: moves to the next piece of the plan (a template or a blueprint) or ends the order. Returns true while
	/// there is a next piece to build.
	///
	/// <para>P1 (multi-build, 03/11/2026): this is where the <b>claims</b> live. The pieces this worker held are freed
	/// (it holds one at a time), and a piece another worker has claimed is <b>skipped</b> - the index moves on, nothing is
	/// paid and it is not counted as done, exactly as the plan requires.</para>
	///
	/// <para>P1 (review A1, 03/11/2026): a worker that reaches the end of the plan does <b>not</b> end the order when the
	/// site still has pieces in its retry queue: it pulls a free index of it (<see cref="MHR_BuildGroup.NextRetry"/>), or
	/// waits for the one another worker is on (<see cref="Phase.WaitRetry"/>). The old per-worker retry pass stays for a
	/// single-worker order (with a group the queue is the site's).</para>
	///
	/// <para>P1 (04/11/2026): the single worker's retry pass waits for <see cref="State.RetryAt"/> - the pieces that need a
	/// support are not walked to again on the same breath - and it is <b>not</b> gated by a number of passes anymore: each
	/// step carries its own real attempts (<see cref="PlannedStep.Tries"/>) and the stall watchdog ends a run that stops
	/// making progress.</para>
	/// </summary>
	private static bool Advance(CompanionAI ai, State state, float dt = 0f)
	{
		int worker = ai.GetInstanceID();
		if (state.Group != null)
		{
			MHR_BuildGroup.ReleaseAllOf(state.Group, worker);
		}
		// 11/11/2026 (the storey barrier): the piece the barrier handed back is done - the walk goes back to the piece it
		// was about to take, WITHOUT re-walking the ones in between (they are already up or skipped). It is restored before
		// the retry pass and the floor reorder so both see the same walk position as the normal path.
		if (state.ResumeIndex >= 0)
		{
			state.Index = state.ResumeIndex - 1;
			state.ResumeIndex = -1;
		}
		if (state.Group == null && state.Plan != null && state.Index + 1 >= state.Plan.Count
			&& state.Retry != null && state.Retry.Count > 0)
		{
			if (Time.time < state.RetryAt)
			{
				state.Phase = Phase.WaitRetry;
				state.Start = Time.time;
				return true;
			}
			state.Passes++;
			List<PlannedStep> again = state.Retry;
			state.Retry = new List<PlannedStep>();
			int first = state.Plan.Count;
			state.Plan.AddRange(again);
			state.Index = first - 1;
			Log(ai, "retry pass " + state.Passes + " with " + again.Count + " piece(s) that did not stand yet");
		}
		if (state.Plan != null && state.Blueprint != null)
		{
			PreferNearFloor(ai, state, state.Index + 1);
		}
		if (state.Plan != null && state.Index + 1 < state.Plan.Count)
		{
			while (state.Index + 1 < state.Plan.Count)
			{
				int next = state.Index + 1;
				// 11/11/2026 (the body barrier, the player's rule: "they may skip, but come back and try again before going
				// up"): while a body is holding a piece of a storey below, the pieces ABOVE it are not taken - the worker
				// waits and the piece that did not go up comes back first (the site's queue keeps it, the claim survives
				// 20 s). The check is on the plan's own level (the blueprint's storey key), never on a second rule.
				// It is an EARLY return on purpose: falling through to the end of Advance would close the order of a
				// single worker (and try the group's end) with the plan still open.
				if (!MHR_BuildGroup.MayTake(state.Group, next))
				{
					// 11/11/2026 (Run 17): one count per continuous hold - the wait is re-asked every frame (Advance is
					// called from inside WaitRetry), so only the frame that ENTERS the wait is a hold of the gate
					if (state.Phase == Phase.WaitRetry)
					{
						// 11/11/2026 (Run 18): the seconds of the gate itself, counted where they happen. This branch is
						// BEFORE ServeBarrier and both return, so it never overlaps the body/dependency seconds.
						state.WaitGate += dt;
					}
					else
					{
						state.BarrierHolds++;
					}
					state.Phase = Phase.WaitRetry;
					state.Start = Time.time;
					return true;
				}
				// 11/11/2026 (the STOREY BARRIER, the player's rule of the Run 9: "they may skip, but come back and try again
				// before going up"): before the plan climbs a storey, the pieces of the queue that are BELOW the next one get
				// their turn back - the entrance steps a body refused, the floor still waiting for its support. It runs
				// BEFORE the claim on purpose: a piece the barrier postpones must not stay claimed by this worker for 20 s.
				if (StoreyBarrier(ai, state, next, dt))
				{
					return true;
				}
				state.Index = next;
				// P1 (review B1, 03/11/2026): a piece the SITE already built is skipped without a claim and WITHOUT
				// walking to it - after a retry (which may be an index behind) the tail of the plan must not be walked
				// again: the ghost check does not see the standing copy (it only tests clipping for m_noClipping), so the
				// duplicate would be paid and placed
				if (state.Group != null && MHR_BuildGroup.IsDone(state.Group, next))
				{
					continue;
				}
				// 11/11/2026 (Run 14, the spread of the crew): the plan's consecutive indices are NEIGHBOURS in space, so
				// five companions all reach for the same corner (88 "corpo na peça" and 32 step-aside asks in the measured
				// log). With a shared site this worker takes the first piece of a small window of the SAME RUN that is FAR
				// from what the others are building (BuildSpread); the pieces it walked past keep their turn in the site's
				// queue (NoteSpread) and come back when that place is free. Single worker, template or the switch off: pick
				// is next, byte for byte the walk of before.
				int pick = next;
				if (SpreadOn(state) && SpreadWindow(state, next, s_spreadWindow, s_spreadPoses) > 1)
				{
					MHR_BuildGroup.ClaimPoses(state.Group, s_spreadClaims);
					int passed;
					int at = BuildSpread.Pick(s_spreadPoses, s_spreadClaims, BuildSpread.SpreadDistance, out passed);
					pick = s_spreadWindow[at];
					if (pick != next)
					{
						state.SpreadSkipped += passed;
						MHR_BuildGroup.NoteSpread(state.Group, s_spreadWindow, passed);
						LogDetailed(ai, "dispersão: a peça " + (next + 1) + "/" + state.Plan.Count
							+ " fica para depois - pegando " + (pick + 1) + " (" + passed + " adiada(s))");
					}
				}
				// P1: a piece another companion is building belongs to it - pass it by (the claim expires in 20 s, and
				// this worker may come back to it through the retry queue)
				if (state.Group != null && !MHR_BuildGroup.TryClaim(state.Group, pick, worker))
				{
					if (CompanionsPlugin.LogNormal)
					{
						LogDebug(ai, "a peça " + (pick + 1) + "/" + state.Plan.Count + " é de outro companheiro - seguindo");
					}
					state.Index = pick;
					continue;
				}
				EnterStep(state, pick);
				Log(ai, "next: " + state.Prefab + " (" + (state.Index + 1) + "/" + state.Plan.Count + ")");
				return true;
			}
		}
		if (state.Blueprint != null)
		{
			// P1 (review A1): the group's retry queue is worked before the order ends - it is the last chance of the
			// pieces that did not stand yet
			if (state.Group != null && state.Plan != null)
			{
				bool spread;
				int retry = MHR_BuildGroup.NextRetry(state.Group, worker, out spread);
				if (retry >= 0 && retry < state.Plan.Count)
				{
					if (spread)
					{
						state.SpreadResumed++;   // 11/11/2026 (the spread of the crew): a piece left for later came back
					}
					EnterStep(state, retry);
					Log(ai, "retry do grupo: " + state.Prefab + " (" + (retry + 1) + "/" + state.Plan.Count + ")");
					return true;
				}
				if (MHR_BuildGroup.RetryPending(state.Group))
				{
					// something is still queued (another worker's, or not due yet) - wait for it instead of leaving
					state.Phase = Phase.WaitRetry;
					state.Start = Time.time;
					return true;
				}
			}
			// P1: the last worker of a group says the number of the WHOLE site (the others end silently - the plan goes on
			// without them)
			if (state.Group != null)
			{
				string key = state.Group;
				// P1 (review A9, 03/11/2026): the group may be gone by now (the last Leave of another worker took it) -
				// then the counters of this order are the honest answer, never a 0/0
				bool live = MHR_BuildGroup.Exists(key);
				// P1 (04/11/2026): the counters are SETS of plan indices (a piece is one piece, whatever it took)
				int placed = live ? MHR_BuildGroup.Placed(key) : state.PlacedIndices.Count;
				int skipped = live ? MHR_BuildGroup.Skipped(key) : state.SkippedIndices.Count;
				MHR_BuildGroup.ReleaseAllOf(key, worker);
				bool last = MHR_BuildGroup.Leave(key, worker);
				state.Group = null;
				if (!last)
				{
					Finish(ai, state, null, "a lista de peças acabou para este companheiro (o grupo continua)");
					return false;
				}
				Finish(ai, state, "hc_ai_blueprint_done", null, state.Blueprint, placed, skipped);
				return false;
			}
			Finish(ai, state, "hc_ai_blueprint_done", null, state.Blueprint, state.PlacedIndices.Count,
				state.SkippedIndices.Count);
			return false;
		}
		if (state.Template != null)
		{
			// P1 (04/11/2026): the line says the pieces that are UP ("The {0} is up: {1} pieces"), not the plan's size
			Finish(ai, state, "hc_ai_build_plan_done", null, state.Template.Label, state.PlacedIndices.Count);
			return false;
		}
		// a single piece
		Finish(ai, state, "hc_ai_build_done", null, LabelOf(state));
		return false;
	}

	/// <summary>P1: makes <paramref name="index"/> the piece this worker is on (the pose, the clocks and the phase - the
	/// same block <see cref="Advance"/> always had, now shared with the follower that starts building).</summary>
	private static void EnterStep(State state, int index)
	{
		PlannedStep next = state.Plan[index];
		state.Index = index;
		state.Prefab = next.Prefab;
		state.Piece = next.Piece;
		state.Pos = next.Pos;
		state.Rot = next.Rot;
		state.Placed = null;
		state.Start = Time.time;
		state.Checked = false;
		state.FloorPreferDone = false;
		state.FallDrops = 0;
		state.BestDistance = 0f;
		state.BestAt = Time.time;
		state.NextScaffoldAt = 0f;
		state.BodyTries = 0;   // 11/11/2026: the short retries of a body are per piece
		state.BodyLogged = false;   // 11/11/2026 (Run 10): the body of the new piece is named once
		state.AsideLogged = false;   // 11/11/2026 (Run 10): and the step-aside of the new piece is asked once in the log
		// 11/11/2026 (Run 10): a new piece ends any step-aside walk - the walk (Goto) is the one steering from here
		state.AsideTarget = Vector3.zero;
		state.AsideUntil = 0f;
		// 11/11/2026 (Run 13): a new piece never reuses the walk spot of the previous one
		state.WalkFor = -1;
		state.HasWalkTo = false;
		state.WalkRays = 0;   // 11/11/2026 (Run 14): the counters of the surface search are the new piece's own
		state.WalkFound = 0;
		state.Phase = Phase.Goto;
	}

	/// <summary>
	/// 11/11/2026 (the <b>storey barrier</b> of the Run 9, the player's rule: "they may skip, but come back and try again
	/// before going up"): the entrance steps (pieces 8-10 of 988) and a whole column of the east side were refused early -
	/// by a body, by the support that was still in the plan - went to the site's queue and only came back in the
	/// end-of-plan retry pass, after the house was already storeys up (Run 9: 977 placed + 11 skipped). This is the gate
	/// that stops the walk from climbing while something of the storey below is still waiting.
	///
	/// <para>Called by <see cref="Advance"/> for the piece it is about to take (<paramref name="nextIndex"/>). It does not
	/// reorder the plan, does not touch claims and does not change indices: it only <b>enters</b> a queued piece below (the
	/// site's queue in a group, <see cref="State.Retry"/> for a single worker) and remembers the index to come back to
	/// (<see cref="State.ResumeIndex"/>).</para>
	///
	/// <para>The barrier is drawn once per <b>next piece</b>: when the walk's target changes (a piece was skipped or another
	/// worker took it) it is redrawn, and it lives at most <see cref="BuildFailure.BodyRetryMaxWait"/> - after that the plan
	/// goes on and the piece stays in the queue for the end-of-plan pass (a player parked on the spot cannot hold the
	/// site). A template (no storeys), a single piece (no plan) and <c>[Building] BodyRetry</c> off are out of it.</para>
	///
	/// <para>11/11/2026 (Run 10, measured): the drawing used to need a <b>climb</b> of a storey (the next piece 1 m above
	/// the current one), and the wall of the 1st floor has the <b>same y</b> as the beam whose tiles were waiting at
	/// 39,8 - the barrier was never drawn and the wall went up over two empty tiles. It is drawn now whenever something is
	/// waiting <b>below</b> the next piece; the patience of the barrier (<see cref="ServeBarrier"/>) is the same clock as
	/// the site's gate (Run 17: one clock - it is never pushed forward, see <see cref="ServeBarrier"/>).</para>
	/// </summary>
	private static bool StoreyBarrier(CompanionAI ai, State state, int nextIndex, float dt)
	{
		if (!BarrierOn(state) || nextIndex < 0 || nextIndex >= state.Plan.Count)
		{
			return false;
		}
		PlannedStep next = state.Plan[nextIndex];
		if (next == null)
		{
			return false;
		}
		float nextY = next.Pos.y;
		if (state.BarrierNextY > 0f && Mathf.Abs(state.BarrierNextY - nextY) > 0.01f)
		{
			ResetBarrier(state);   // the walk's target changed (a piece done/claimed): the barrier is drawn for this one
		}
		if (state.BarrierNextY <= 0f)
		{
			// 11/11/2026 (Run 10): the barrier used to exist only while the plan CLIMBED a storey (1 m up) - and the wall of
			// the 1st floor has the SAME y as the beam whose tiles were waiting at 39,8, so the pending piece below was
			// never seen and the wall went up over two empty tiles. Now anything waiting below the piece the walk is about
			// to take draws the barrier, whatever the walk's own height is.
			int waiting = PendingBelowIndex(ai, state, nextIndex, nextY);
			if (waiting < 0)
			{
				return false;   // nothing of the storey below is waiting: just climb
			}
			state.BarrierNextY = nextY;
			state.BarrierNextIndex = nextIndex;
			state.BarrierStart = Time.time;
			state.BarrierServed.Clear();
			LogDebug(ai, "barreira de andar: " + state.Plan[waiting].Prefab + " (" + (waiting + 1) + "/"
				+ state.Plan.Count + ") espera embaixo - nada acima de " + F(nextY) + " m sobe antes dela");
		}
		// 11/11/2026 (Run 17): the frame this barrier is DRAWN already returns to the wait, and the seconds are the
		// caller's dt. 11/11/2026 (Run 18): it used to be 0f, so the drawing frame counted nothing at all - Advance now
		// carries the dt of the wait down to here.
		return ServeBarrier(ai, state, dt);
	}

	/// <summary>
	/// 11/11/2026 (the storey barrier): the part of it that both paths share - <see cref="StoreyBarrier"/> (the walk about to
	/// climb) and <see cref="WaitRetry"/> (already waiting). It <b>serves</b> the lowest queued piece below the barrier as
	/// soon as it is due, <b>waits</b> while it is still coming (not due, or another worker holds it) and <b>drops the
	/// barrier</b> when there is nothing below anymore or its patience is over - the normal path then goes on. True = the
	/// barrier is holding the plan (a piece was entered or the worker waits).
	/// </summary>
	private static bool ServeBarrier(CompanionAI ai, State state, float dt)
	{
		if (state == null || state.Plan == null || state.BarrierNextY <= 0f || state.BarrierNextIndex < 0
			|| state.BarrierNextIndex >= state.Plan.Count)
		{
			return false;
		}
		if (ModConfig.BuildBodyRetry != null && !ModConfig.BuildBodyRetry.Value)
		{
			ResetBarrier(state);   // the player turned it off in the F8 panel while the order was running
			return false;
		}
		// 11/11/2026 (Run 17, ONE CLOCK): the patience runs from the drawing of the barrier and is never pushed forward.
		// INVARIANT: ServeBarrier never outlives the site's gate (MHR_BuildGroup.MayTake) - BodyRetryMaxWait IS
		// BodyBarrierSeconds (10 s), so the moment the gate lets the storeys above be taken the wait is over too. The old
		// push (while nothing had been served and the piece below was not due yet) made the patience indefinite, and the
		// 25 s that followed were longer than the 20 s of the gate: the gate opened, the wait held the worker anyway, and
		// the longer clock always won (Run 16: 31 "nothing above ... is taken" lines with workers standing still).
		if (Time.time - state.BarrierStart > BuildFailure.BodyRetryMaxWait)
		{
			int stuck = PendingBelowIndex(ai, state, state.BarrierNextIndex, state.BarrierNextY);
			if (stuck < 0 || stuck >= state.Plan.Count)
			{
				stuck = state.BarrierNextIndex;
			}
			PlannedStep piece = state.Plan[stuck];
			Log(ai, "a peça " + (piece != null ? piece.Prefab : state.Prefab) + " travada não voltou - o plano sobe depois de "
				+ BuildFailure.BodyBarrierSeconds.ToString("F0") + " s");
			ResetBarrier(state);
			return false;   // the normal gate: the plan climbs
		}
		int serve = NextRetryBelow(ai, state, state.BarrierNextIndex, state.BarrierNextY);
		if (serve >= 0)
		{
			state.BarrierServed.Add(serve);
			state.ResumeIndex = state.BarrierNextIndex;
			EnterStep(state, serve);
			Log(ai, "volta antes de subir: " + state.Prefab + " (" + (serve + 1) + "/" + state.Plan.Count + ") em ("
				+ F(state.Pos.x) + ", " + F(state.Pos.y) + ", " + F(state.Pos.z) + ")");
			return true;
		}
		int below = PendingBelowIndex(ai, state, state.BarrierNextIndex, state.BarrierNextY);
		if (below >= 0)
		{
			// 11/11/2026 (Run 17, the wait instrumented): the seconds the plan stood still here, counted apart - a body and
			// a dependency are different problems and the A/B needs the two numbers to tell which one cost the time.
			// One count per continuous hold - the wait is re-asked every frame, so only the frame that ENTERS it counts
			// (the seconds below accumulate every frame: those are the real wait).
			if (state.Phase != Phase.WaitRetry)
			{
				state.BodyHolds++;
			}
			if (BlockedBelow(state, below))
			{
				state.WaitBody += dt;
			}
			else
			{
				state.WaitDependency += dt;
			}
			state.Phase = Phase.WaitRetry;   // it is coming: wait below the storey instead of climbing over it
			state.Start = Time.time;
			return true;
		}
		ResetBarrier(state);
		return false;   // nothing below: the plan climbs
	}

	/// <summary>11/11/2026: the storey barrier is only for a <b>blueprint</b> of this game (<c>[Building] BodyRetry</c>): a
	/// template has no storeys to climb and a single piece has no plan to walk.</summary>
	private static bool BarrierOn(State state)
	{
		if (state == null || state.Plan == null || state.Blueprint == null || state.Index < 0)
		{
			return false;
		}
		if (state.Group == null && state.Retry == null)
		{
			return false;   // a single piece: there is no queue to come back to
		}
		return ModConfig.BuildBodyRetry == null || ModConfig.BuildBodyRetry.Value;
	}

	/// <summary>11/11/2026: drops the storey barrier and forgets the pieces it already served (the next one gives them another
	/// turn).</summary>
	private static void ResetBarrier(State state)
	{
		if (state == null)
		{
			return;
		}
		state.BarrierNextY = 0f;
		state.BarrierNextIndex = -1;
		state.BarrierStart = 0f;
		state.BarrierServed.Clear();
	}

	/// <summary>
	/// 11/11/2026 (the storey barrier): the plan index of a piece of the queue that is still <b>below</b> the next piece, or -1
	/// (the site's queue in a group, this worker's list otherwise). It is the "is something waiting below?" question of
	/// <see cref="StoreyBarrier"/> - and the index is what lets the log name the piece that did not come back.
	/// </summary>
	private static int PendingBelowIndex(CompanionAI ai, State state, int nextIndex, float nextY)
	{
		if (state == null || state.Plan == null)
		{
			return -1;
		}
		if (state.Group != null)
		{
			return MHR_BuildGroup.PendingBelow(state.Group, nextIndex, nextY, state.BarrierServed);
		}
		if (state.Retry == null)
		{
			return -1;
		}
		int best = -1;
		float bestY = float.MaxValue;
		for (int i = 0; i < state.Retry.Count; i++)
		{
			int index = BelowIndex(ai, state, state.Retry[i], nextIndex, nextY);
			if (index < 0)
			{
				continue;
			}
			float y = state.Plan[index].Pos.y;
			if (best < 0 || y < bestY)
			{
				best = index;
				bestY = y;
			}
		}
		return best;
	}

	/// <summary>
	/// 11/11/2026 (Run 17, the wait instrumented): was the queued piece of this plan index refused by a <b>body</b> at least
	/// once? The site keeps the count for a group (<c>MHR_BuildGroup.BlockedOf</c>), the single worker keeps it on the
	/// plan's own step (<see cref="PlannedStep.Blocked"/>). It is only the question the summary line answers - nothing in
	/// the gate reads it.
	///
	/// <para>11/11/2026 (Run 18): in a <b>group</b> the question is the <b>age</b> of the stamp (<c>BodyAt</c>, the same
	/// clock <c>MayTake</c> reads), never the piece's whole life - <c>Blocked</c> is never reset, so one body that walked
	/// over the spot once answered "body" for every later wait of that piece and deflated the dependency number.</para>
	/// </summary>
	private static bool BlockedBelow(State state, int index)
	{
		if (state == null || state.Plan == null || index < 0 || index >= state.Plan.Count)
		{
			return false;
		}
		if (state.Group != null)
		{
			return MHR_BuildGroup.BlockedRecently(state.Group, index);
		}
		PlannedStep step = state.Plan[index];
		return step != null && step.Blocked > 0;
	}

	/// <summary>
	/// 11/11/2026 (the storey barrier): the queued piece the barrier hands back <b>now</b> - the lowest one that is due (the
	/// site's <c>At</c> in a group, <see cref="State.RetryAt"/> for a single worker), not served yet and not held by
	/// anybody else. -1 when nothing below is ready. In a group the index is <b>claimed</b> here, exactly like
	/// <c>MHR_BuildGroup.NextRetry</c>, so two workers never pull the same piece.
	/// </summary>
	private static int NextRetryBelow(CompanionAI ai, State state, int nextIndex, float nextY)
	{
		if (ai == null || state == null || state.Plan == null)
		{
			return -1;
		}
		if (state.Group != null)
		{
			return MHR_BuildGroup.NextRetryBelow(state.Group, nextIndex, nextY, state.BarrierServed, ai.GetInstanceID());
		}
		if (state.Retry == null || Time.time < state.RetryAt)
		{
			return -1;   // nothing queued, or the dependency/body clock has not passed yet
		}
		int best = -1;
		float bestY = float.MaxValue;
		for (int i = 0; i < state.Retry.Count; i++)
		{
			int index = BelowIndex(ai, state, state.Retry[i], nextIndex, nextY);
			if (index < 0)
			{
				continue;
			}
			float y = state.Plan[index].Pos.y;
			if (best < 0 || y < bestY)
			{
				best = index;
				bestY = y;
			}
		}
		return best;
	}

	/// <summary>
	/// 11/11/2026 (the storey barrier, single worker): the plan index of this queued step when it is a piece <b>below</b> the
	/// next one that the barrier may serve - the same window as <c>MHR_BuildGroup.IsBelow</c>: before <paramref name="nextIndex"/>
	/// in the plan, below <paramref name="nextY"/>, not done, not given up, not served yet and <b>not a door</b> (class 4 -
	/// doors stay last). A step whose pose was adjusted is a <b>copy outside the plan</b> (see <c>AdjustFallenPose</c>): it has
	/// no index to come back to and stays for the end-of-plan pass.
	/// </summary>
	private static int BelowIndex(CompanionAI ai, State state, PlannedStep step, int nextIndex, float nextY)
	{
		if (step == null)
		{
			return -1;
		}
		int index = state.Plan.IndexOf(step);
		if (index < 0)
		{
			LogDebug(ai, "a peça " + step.Prefab + " da fila não está no plano (pose ajustada) - fica para o fim");
			return -1;
		}
		if (index >= nextIndex || state.PlacedIndices.Contains(index) || state.SkippedIndices.Contains(index)
			|| state.BarrierServed.Contains(index))
		{
			return -1;
		}
		PlannedStep planned = state.Plan[index];
		if (planned == null || planned.Pos.y >= nextY - 0.01f)
		{
			return -1;   // same storey or above: not what the barrier settles
		}
		return BuildBlueprintParser.ClassOf(planned.Prefab) == 4 ? -1 : index;
	}

	/// <summary>
	/// P1 (multi-build, 03/11/2026): the plan belongs to the <b>group</b> and every worker walks it by <b>index</b>, so a
	/// reorder made by one worker would leave the others' indices pointing at other pieces (and their claims with them).
	/// While the site has more than one worker nobody reorders it - the retry queue builds what the reorder used to fix.
	/// </summary>
	private static bool PlanLocked(State state)
	{
		return state != null && state.Group != null && MHR_BuildGroup.IsShared(state.Group);
	}

	/// <summary>
	/// P1 (review A2, 03/11/2026): the leader is the <b>site's</b>, not the state's - when it leaves (a gather, a death,
	/// "para, Fulano") the next worker takes the site and a flag copied at the start would lie. Without a group the
	/// companion is its own leader (the single-worker order of 03/11/2026 is unchanged).
	/// </summary>
	private static bool IsLeader(CompanionAI ai, State state)
	{
		if (state == null)
		{
			return false;
		}
		if (state.Group == null)
		{
			return true;
		}
		return ai != null && MHR_BuildGroup.IsLeader(state.Group, ai.GetInstanceID());
	}

	/// <summary>
	/// P1 (review A7, 03/11/2026): one piece is <b>counted once</b> - the site remembers the index it was done for, so a
	/// piece built by another worker (or a verdict asked twice) does not inflate the numbers of the order.
	///
	/// <para>P1 (04/11/2026, "contadores honestos"): the order's own counters are sets of plan indices as well, and a piece
	/// that had been given up and then went up is <b>placed</b>, never both - it leaves the skipped set.</para>
	/// </summary>
	private static void CountPlaced(State state)
	{
		if (state == null)
		{
			return;
		}
		int index = PieceIndex(state);
		if (index >= 0)
		{
			state.PlacedIndices.Add(index);
			state.SkippedIndices.Remove(index);
			if (state.Group != null)
			{
				MHR_BuildGroup.MarkDone(state.Group, index);
			}
		}
		state.LastProgressAt = Time.time;
	}

	/// <summary>
	/// P1 (review A11, 03/11/2026): the companion a component belongs to, resolved <b>once</b> - the guards of the jobs
	/// ask the same GameObject on every frame and a second <c>GetComponent</c> there was pure waste.
	/// </summary>
	private static CompanionAI AIOf(Component c)
	{
		if (c == null)
		{
			return null;
		}
		CompanionAI ai = c as CompanionAI;
		return ai != null ? ai : c.GetComponent<CompanionAI>();
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
			// E7 (11/2026): the two skip rules - and the words that explain them - live in BuildBlueprintSkip, which is
			// pure and shared with the DevBridge's blueprints.inspect: the report and this log cannot disagree. The
			// recipe is only asked of a resolved piece (local may be null with no player in the world).
			string skip = BuildBlueprintSkip.Reason(
				resolved != null,
				local == null || (resolved != null && local.IsRecipeKnown(resolved.m_name)));
			if (skip != null)
			{
				if (resolved == null && missingPrefab == null)
				{
					missingPrefab = piece.Prefab;
				}
				if (firstSkip == null)
				{
					firstSkip = piece.Prefab + " (" + skip + ")";
				}
				skipped++;
				continue;
			}
			Vector3 placed = hint + rot * Vector3.Scale(piece.Pos - centre, new Vector3(piece.Scale, piece.Scale, piece.Scale));
			plan.Add(new PlannedStep
			{
				Prefab = piece.Prefab,
				Piece = resolved,
				Pos = placed,
				Rot = rot * piece.Rot,
				Level = piece.Level
			});
		}
		if (plan.Count == 0)
		{
			CompanionsPlugin.Log?.LogWarning("[Build] blueprint " + blueprint.Label + ": nothing buildable here ("
				+ skipped + " line(s) skipped, first '" + (missingPrefab ?? "?") + "')");
			return BlueprintStart.Failed;
		}
		// P1 (04/11/2026, player's request: "the whole build should be raised, not only the entrance stairs - the ground
		// floor should sit a little above the ground, on the poles; it is like a deck"): the ground floor rises by
		// [Building] DeckLift (default 1 m) so it sits on the poles the author drew, with the stairs reaching it.
		//
		// The plan may NOT follow its own lowest piece: a blueprint whose poles are buried 2 m has its lowest piece 2 m
		// under the floor, so "raise by the lowest" lifted the WHOLE house 2,00 m (measured in the run of 04/11/2026) -
		// the player asked for a deck, not a tower. What is left is the pole TIPS: a piece still below the terrain after
		// the lift is put ON the terrain (its own x/z, so each pole finds its own ground) instead of being buried or
		// floating - the buried foundation is what the game refuses, and every piece above it fails in a cascade (the
		// east hole of that run).
		//
		// 11/11/2026 (Run 19, the floating corpus): the config is a CEILING, not a forced offset. It used to be added to
		// every plan, so the five flat blueprints of the corpus (Meadow Stilt Hut, Forest Hut, Meadow House, Meadow Manor,
		// Hobbit Repairhut) ended 1,00 m above a flat terrain with nothing holding the floor up - measured: ground 38,78,
		// lowest piece 39,78. What a plan needs is the depth of its deepest piece under the terrain: nothing buried needs
		// NO lift, poles buried 2 m keep the 1 m of the config and the loop below settles the rest of the tips.
		if (plan.Count > 0)
		{
			float wanted = ModConfig.BuildDeckLift != null ? ModConfig.BuildDeckLift.Value : DeckLiftDefault;
			float buried = 0f;   // how deep the deepest piece of the plan is UNDER the terrain
			if (ZoneSystem.instance != null)
			{
				for (int i = 0; i < plan.Count; i++)
				{
					float depth = ZoneSystem.instance.GetGroundHeight(plan[i].Pos) - plan[i].Pos.y;   // > 0 = underground
					if (depth > buried)
					{
						buried = depth;
					}
				}
			}
			float lift = Mathf.Clamp(buried, 0f, wanted);   // never rises more than the plan actually needs
			// A piece a few centimetres under the ground is settled by the loop below anyway (same 0.05 m), so it must
			// not cap the whole plan at 0.03 m - that would log a lift nobody asked for (review of the Run 19 patch).
			if (lift < 0.05f)
			{
				lift = 0f;
			}
			int grounded = 0;
			for (int i = 0; i < plan.Count; i++)
			{
				Vector3 pos = plan[i].Pos;
				pos.y += lift;
				if (ZoneSystem.instance != null)
				{
					float ground = ZoneSystem.instance.GetGroundHeight(pos);
					if (pos.y < ground - 0.05f)
					{
						pos.y = ground;   // the pole tip stands on the ground instead of disappearing under it
						grounded++;
					}
				}
				plan[i].Pos = pos;
			}
			bool capped = lift < wanted - 0.001f;
			if (lift > 0.001f || grounded > 0 || capped)
			{
				// 11/11/2026 (Run 19): when the config was CAPPED the line says both numbers - "deck pedido" is what the
				// config asks and the first number is what the plan needed; the buried depth is what decided it, so a flat
				// plan reads "nada enterrado" instead of hiding why the house is on the ground. Uncapped, the line is the
				// one of before, byte for byte (the frozen run of the MACCommHomeBasicBP greps it).
				string line = "[Build] blueprint " + blueprint.Label + ": a obra sobe " + lift.ToString("F2") + " m";
				line += capped ? " (deck pedido " + wanted.ToString("F2") + " m)" : " (deck)";
				line += " - " + grounded + " peça(s) de fundação assentadas no terreno";
				if (capped)
				{
					line += buried > 0.001f ? ", " + buried.ToString("F2") + " m enterrado(s)" : ", nada enterrado";
				}
				CompanionsPlugin.Log?.LogInfo(line);
			}
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
		// Lote 1 (player's request, 11/11/2026): with [Building] BlueprintStations on (the default) a blueprint is built
		// WITHOUT stations. The switch is the player's: off = the vanilla rule, exactly as before (below unchanged).
		bool stationNeeded = StationRequired(true);
		if (!resuming)
		{
			if (stationNeeded)
			{
				float extent = hasBounds ? Mathf.Max(maxX - minX, maxZ - minZ) : 0f;
				PlanStations(cs, plan, hint, extent * 0.5f + 2.5f, rot, Self(ai), ai);
			}
			else
			{
				// the pass that plans workbenches is NOT called (it would insert benches in the player's house): the one
				// honest line replaces its summary, and it says how many pieces would have needed one
				int stationPieces = 0;
				foreach (PlannedStep step in plan)
				{
					if (step.Piece != null && step.Piece.m_craftingStation != null)
					{
						stationPieces++;
					}
				}
				CompanionsPlugin.Log?.LogInfo("[Build] blueprint " + blueprint.Label + ": estações não exigidas por config ("
					+ stationPieces + " peça(s) que precisariam)");
			}
		}
		// a station this build cannot make by itself (stonecutter, forge, black forge...) is still a refusal, and it names
		// the station instead of dying at the piece. Waived (lote 1) = nothing is checked here: no piece of this
		// blueprint is refused for a station.
		if (stationNeeded)
		{
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

	/// <summary>
	/// P1 (multi-build, 03/11/2026): starts the SAME blueprint on <b>several</b> companions - one plan, distinct pieces.
	/// It is the entry point of the order layer (<c>CompanionOrders.Build</c>): "build with no name" is every companion
	/// that can take the order, "build, Maria and João" is the named ones; the first of the list is the <b>leader</b>.
	///
	/// <para>The leader starts the real order (it levels the ground, clears the site and plans the workbenches with
	/// <see cref="StartBlueprint"/>) and its resolved plan becomes the plan of the <b>group</b>. The others start from
	/// that exact plan (<c>storedPlan</c> - the same path the gather/death resume uses, so the indices line up) in
	/// <see cref="Phase.WaitGroup"/>: they do nothing at all until the leader opens
	/// <see cref="MHR_BuildGroup.BuildPhase"/>, and then they claim pieces of their own.</para>
	///
	/// <para>Returns how many companions entered the order (0 = refused). <paramref name="missingPrefab"/> and
	/// <paramref name="missingStation"/> come from the leader's start (the names the player has to hear).</para>
	/// </summary>
	internal static int StartBlueprintGroup(IEnumerable<CompanionSetup> workers, Blueprint blueprint, Vector3 hint,
		out string missingPrefab, out string missingStation)
	{
		missingPrefab = null;
		missingStation = null;
		if (workers == null || blueprint == null)
		{
			return 0;
		}
		List<CompanionSetup> list = new List<CompanionSetup>();
		foreach (CompanionSetup cs in workers)
		{
			if (cs == null || list.Contains(cs) || cs.GetComponent<CompanionAI>() == null)
			{
				continue;
			}
			Character body = cs.GetComponent<Character>();
			if (body != null && body.IsDead())
			{
				continue;   // a dead companion cannot take the order (it would only be a missing worker)
			}
			list.Add(cs);
		}
		if (list.Count == 0)
		{
			return 0;
		}
		CompanionSetup leader = list[0];
		long owner = MHR_Owner.CreatorId(leader);
		CompanionAI leaderAi = leader.GetComponent<CompanionAI>();
		// P1: ONE site per player at a time (§3) - a second order would put two leaders over the same ground (or the same
		// plan with new indices). Said BEFORE anything is started, so a running site is not disturbed.
		if (MHR_BuildGroup.SiteBusy(owner))
		{
			Log(leaderAi, "já existe um grupo de obra em andamento - diga \"para\" antes de começar outra");
			return 0;
		}
		if (list.Count == 1)
		{
			// one companion does not need a group: the order is exactly the one of 03/11/2026 (no claims, no waiting)
			BlueprintStart single = StartBlueprint(leader, blueprint, hint, out missingPrefab, out missingStation);
			return single == BlueprintStart.Failed ? 0 : 1;
		}
		// the key is built from the SAME pieces StartBlueprint uses for the anchor and the rotation (the aim of the
		// owner), so the leader's state and the group agree on the site
		Player ownerPlayer = MHR_Owner.Of(leader);
		Vector3 forward = ownerPlayer != null ? ownerPlayer.transform.forward : leader.transform.forward;
		Quaternion rot = Quaternion.LookRotation(Flat(forward), Vector3.up);
		string key = MHR_BuildGroup.KeyOf(blueprint.Label, hint, rot, owner);
		BlueprintStart started = StartBlueprint(leader, blueprint, hint, out missingPrefab, out missingStation);
		if (started == BlueprintStart.Failed)
		{
			return 0;
		}
		State first = null;
		if (leaderAi != null)
		{
			s_state.TryGetValue(leaderAi.GetInstanceID(), out first);
		}
		if (started == BlueprintStart.Leveling || first == null || first.Plan == null || first.Plan.Count == 0)
		{
			if (started == BlueprintStart.Leveling)
			{
				// P1 (review A5, 03/11/2026): the hoe job took the site first (the ground comes before the plan) - it
				// carries the WHOLE group, so the build comes back with all of them and the plan is created then, with a
				// flat site (the player's site measured 0,37 m against the 0,35 m tolerance: this path IS the normal one)
				MHR_Level.SetThenWorkers(leader, NamesOf(list));
				Log(leaderAi, "o nivelamento vem primeiro - os " + list.Count
					+ " companheiro(s) voltam quando o chão estiver pronto");
				return list.Count;
			}
			Log(leaderAi, "o plano do canteiro não ficou pronto - o líder segue sozinho");
			return 1;
		}
		if (MHR_BuildGroup.TryCreate(key, blueprint.Label, owner, first.Plan, leaderAi.GetInstanceID()))
		{
			first.Group = key;
			MHR_BuildGroup.SetBuildPhase(key, false);   // the site belongs to the leader until it is ready
		}
		else
		{
			// the key is taken (SiteBusy already refused the other cases): the leader builds alone, it is the order of
			// 03/11/2026 unchanged
			Log(leaderAi, "não deu para abrir o grupo deste canteiro - o líder constrói sozinho");
			return 1;
		}
		int joined = 1;
		List<PlannedStep> groupPlan = MHR_BuildGroup.Plan(key) ?? first.Plan;
		for (int i = 1; i < list.Count; i++)
		{
			CompanionSetup follower = list[i];
			CompanionAI ai = follower.GetComponent<CompanionAI>();
			if (ai == null)
			{
				continue;
			}
			string why;
			if (!JoinSite(follower, key, out why))
			{
				Log(ai, "não entrou no grupo da obra (" + why + ") - fica de fora");
				continue;
			}
			joined++;
		}
		Log(leaderAi, "grupo de obra \"" + blueprint.Label + "\": " + joined + " companheiro(s) no mesmo plano ("
			+ groupPlan.Count + " peças)");
		return joined;
	}

	/// <summary>
	/// P1 (multi-build, 03/11/2026) + 11/11/2026 (the build panel): puts <b>one more</b> companion on a running site -
	/// the follower block of <see cref="StartBlueprintGroup"/>, extracted so the panel can add a companion to a site that
	/// is already up (and so a follower of a fresh order and one of the panel start exactly the same way).
	///
	/// <para>It resumes the plan of the site (<c>StartBlueprint</c> with the stored plan: the same anchor, the same
	/// rotation, the same pieces, no leveling, no clearing, no station pass - the leader did all three), joins the group
	/// and waits in <see cref="Phase.WaitGroup"/> for <c>MHR_BuildGroup.BuildPhase</c>. The pose and the file of the site
	/// are the LEADER's state: without a leader with a plan there is nothing to join.</para>
	///
	/// <para><paramref name="reason"/> is a short token for the log/panel when it refuses: <c>no companion</c>,
	/// <c>no site</c>, <c>it's already on the site</c>, <c>it's dead</c>, <c>it's the leader</c>,
	/// <c>the site is paused</c>, <c>the site has no plan</c>, <c>the blueprint is gone</c>,
	/// <c>could not start the plan</c>.</para>
	/// </summary>
	internal static bool JoinSite(CompanionSetup cs, string key, out string reason)
	{
		reason = null;
		if (cs == null)
		{
			reason = "no companion";
			return false;
		}
		CompanionAI ai = cs.GetComponent<CompanionAI>();
		if (ai == null)
		{
			reason = "no companion";
			return false;
		}
		if (!MHR_BuildGroup.Exists(key))
		{
			reason = "no site";
			return false;
		}
		if (MHR_BuildGroup.Worker(ai.GetInstanceID()))
		{
			// joining twice would restart its own state (StartBlueprint discards it) and throw away the piece it is on
			reason = "it's already on the site";
			return false;
		}
		Character body = cs.GetComponent<Character>();
		if (body != null && body.IsDead())
		{
			reason = "it's dead";
			return false;
		}
		if (MHR_BuildGroup.IsLeader(key, ai.GetInstanceID()))
		{
			reason = "it's the leader";
			return false;
		}
		if (MHR_BuildGroup.Paused(key))
		{
			// a paused site is not taking new workers: it would start walking the moment it joined (the pause is the
			// site's, and the panel resumes the whole site)
			reason = "the site is paused";
			return false;
		}
		int leaderId = MHR_BuildGroup.LeaderId(key);
		State lead;
		if (leaderId == 0 || !s_state.TryGetValue(leaderId, out lead) || lead == null || string.IsNullOrEmpty(lead.BlueprintFile))
		{
			reason = "the site has no plan";
			return false;
		}
		Blueprint blueprint = BuildBlueprints.Load(lead.BlueprintFile);
		if (blueprint == null)
		{
			reason = "the blueprint is gone";
			return false;
		}
		List<PlannedStep> groupPlan = MHR_BuildGroup.Plan(key) ?? lead.Plan;
		if (groupPlan == null || groupPlan.Count == 0)
		{
			reason = "the site has no plan";
			return false;
		}
		string ignoredPrefab;
		string ignoredStation;
		// `resuming` + the stored plan: the SAME anchor, the same rotation and the SAME pieces, with no leveling, no
		// clearing and no station pass (the leader already did all three)
		if (StartBlueprint(cs, blueprint, lead.Hint, out ignoredPrefab, out ignoredStation, 0, lead.AnchorRot, true,
			groupPlan) != BlueprintStart.Started)
		{
			reason = "could not start the plan";
			return false;
		}
		State follow;
		if (!s_state.TryGetValue(ai.GetInstanceID(), out follow) || follow == null)
		{
			reason = "could not start the plan";
			return false;
		}
		follow.Group = key;
		follow.Plan = groupPlan;   // the very list of the leader (index by index)
		follow.Phase = Phase.WaitGroup;
		follow.Start = Time.time;
		ai.StopMoving();
		MHR_Mover.Release(ai);
		MHR_BuildGroup.TryJoin(key, ai.GetInstanceID());
		Log(ai, "entrou no grupo da obra \"" + blueprint.Label + "\" - espera o líder preparar o canteiro");
		return true;
	}

	/// <summary>P1 (review A5, 03/11/2026): the names of the group of an order - the leveling job carries them (a
	/// reference to a companion does not survive a death/respawn, a name does).</summary>
	internal static List<string> NamesOf(List<CompanionSetup> workers)
	{
		List<string> names = new List<string>();
		if (workers == null)
		{
			return names;
		}
		for (int i = 0; i < workers.Count; i++)
		{
			string name = workers[i] != null ? CompanionNames.NameOf(workers[i]) : null;
			if (!string.IsNullOrEmpty(name) && !names.Contains(name))
			{
				names.Add(name);
			}
		}
		return names;
	}

	/// <summary>P1 (review A5, 03/11/2026): the companions of that list, with <paramref name="leader"/> <b>first</b> (it
	/// is the one that held the hoe and the one that starts the plan). A companion that is not around anymore (or is
	/// dead) is simply not in the group.</summary>
	internal static List<CompanionSetup> GroupWorkers(List<string> names, CompanionSetup leader)
	{
		List<CompanionSetup> list = new List<CompanionSetup>();
		if (leader != null)
		{
			list.Add(leader);
		}
		if (names == null || names.Count == 0)
		{
			return list;
		}
		foreach (CompanionSetup cs in CompanionSetup.AllCompanions)
		{
			if (cs == null || cs == leader)
			{
				continue;
			}
			Character body = cs.GetComponent<Character>();
			if (body != null && body.IsDead())
			{
				continue;
			}
			string name = CompanionNames.NameOf(cs);
			if (!string.IsNullOrEmpty(name) && names.Contains(name))
			{
				list.Add(cs);
			}
		}
		return list;
	}

	internal static bool Active(Component c)
	{
		// P1 (review A11, 03/11/2026): the companion is resolved ONCE - this guard is asked by every job, every frame
		CompanionAI ai = AIOf(c);
		if (ai == null)
		{
			return false;
		}
		State state;
		if (s_state.TryGetValue(ai.GetInstanceID(), out state) && state != null && state.Active)
		{
			return true;
		}
		// P1 (multi-build): a worker of a group answers as an order too, even before its state is resolved (a resumed
		// body joins the group and the idle job must not take it in the meantime)
		return MHR_BuildGroup.Worker(ai.GetInstanceID());
	}

	/// <summary>E7: this companion's build order is <b>clearing the site</b> - the harvest is the tool then, so it is the
	/// only job allowed to run (it must not be held still by the order). 11/11/2026 (the build panel): not while the
	/// order is <b>paused</b> - a pause stops the work, the cutting included.</summary>
	internal static bool Clearing(Component c)
	{
		State state;
		return StateOf(c, out state) && state.Active && state.Phase == Phase.Clear && !IsPaused(state);
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
		CompanionAI ai = AIOf(c);
		return ai != null && s_state.TryGetValue(ai.GetInstanceID(), out state) && state != null;
	}

	internal static void Cancel(Component c)
	{
		CompanionAI owner = AIOf(c);
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
			// P1 (multi-build): "para, Maria" takes HER out of the group and frees her piece - the others go on (the group
			// is dropped only when the last one leaves; the next worker takes the site)
			if (state.Group != null)
			{
				MHR_BuildGroup.ReleaseAllOf(state.Group, owner.GetInstanceID());
				MHR_BuildGroup.Leave(state.Group, owner.GetInstanceID());
				state.Group = null;
			}
			MHR_Mover.Release(owner);
			ReleaseHarvest(owner, state);
		}
		// E7 (review, 03/11/2026) + 11/11/2026 (the build panel): the pending falls even when there is NO active state -
		// the gather handover removes the state and leaves only the watcher, and "para" used to leave it alive (the
		// build came back by itself as soon as the bag was covered). Its key is the instance id, which Unity reuses.
		if (s_pending.TryGetValue(owner.GetInstanceID(), out pending))
		{
			s_pending.Remove(owner.GetInstanceID());
		}
		// E6b: the gather turned Follow off - another order cancels the build and Follow has to come back
		RestoreFollow(owner, c.GetComponent<CompanionSetup>(), pending);
	}

	internal static void CancelAll()
	{
		// P1 (multi-build): "para" without a name drops every site (the claims go with it - a piece must never stay
		// claimed by an order that no longer exists)
		MHR_BuildGroup.CancelAll();
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
	/// 11/11/2026 (the build panel): <b>pauses</b> this companion's build without losing anything. The state stays alive
	/// (<c>Drive</c> answers "still running" and does nothing, so no idle job takes the body) with the same plan, the same
	/// piece and the same counters; the mover and the harvest are released so the companion stands still, and the
	/// temporary stairs come down (they are a tool of the work, not of the pause). Nothing is claimed, released or
	/// finished: a pause is not a "para" - the claims simply expire in <c>MHR_BuildGroup.ClaimSeconds</c> if the pause is
	/// long, which is exactly what a live worker that went away looks like.
	/// <para>A companion that is only <b>waiting for materials</b> (the gather handed the order over and there is no
	/// state) is parked: Follow comes back, the order cap and the work mode are cleared - without that it kept chopping
	/// wood with nothing to do (the bug of <c>DropPending</c>, which never restored the mode).</para>
	/// <para>A site (group) is paused as a whole: the leader's pause marks the group, so a worker that is added later or
	/// comes back from a gather does not walk into a paused site.</para>
	/// </summary>
	internal static void Pause(Component c)
	{
		CompanionAI ai = AIOf(c);
		if (ai == null)
		{
			return;
		}
		CompanionSetup cs = ai.GetComponent<CompanionSetup>();
		State state;
		if (s_state.TryGetValue(ai.GetInstanceID(), out state) && state != null && state.Active)
		{
			if (state.Group != null)
			{
				MHR_BuildGroup.SetPaused(state.Group, true);
			}
			if (state.Paused)
			{
				return;   // already paused (the panel asks for every worker of the site)
			}
			state.Paused = true;
			ai.StopMoving();
			MHR_Mover.Release(ai);
			ReleaseHarvest(ai, state);
			ClearScaffolds(ai, state);
			// the event's line is the SITE's (PauseSite); this one is the detail of the worker
			LogDebug(ai, "obra pausada: " + LabelOf(state) + " (peça " + (state.Index + 1) + "/"
				+ (state.Plan != null ? state.Plan.Count : 1) + ")");
			return;
		}
		Pending pending;
		if (s_pending.TryGetValue(ai.GetInstanceID(), out pending) && pending != null)
		{
			ParkPending(ai, cs, pending);
		}
	}

	/// <summary>
	/// 11/11/2026 (the build panel): lifts the pause. A paused <b>state</b> goes back to the very piece it was on: the
	/// clocks that decide "it is stuck" are reset (the same four <c>WaitGroup</c> resets) and the claim is taken again -
	/// when another worker holds the piece (or the site's queue moved on) the walk advances, exactly like a companion
	/// that comes back to its site (<see cref="RejoinGroup"/>). A paused <b>pending</b> is resumed through
	/// <see cref="ResumePending"/>, the very path of a gather that finished.
	/// </summary>
	internal static void Resume(Component c)
	{
		CompanionAI ai = AIOf(c);
		if (ai == null)
		{
			return;
		}
		CompanionSetup cs = ai.GetComponent<CompanionSetup>();
		State state;
		if (s_state.TryGetValue(ai.GetInstanceID(), out state) && state != null && state.Active)
		{
			if (state.Group != null)
			{
				MHR_BuildGroup.SetPaused(state.Group, false);
			}
			if (!state.Paused)
			{
				return;   // not paused (the panel asks for every worker of the site)
			}
			state.Paused = false;
			state.Checked = false;
			state.FallDrops = 0;
			state.BestDistance = 0f;
			state.BestAt = Time.time;
			LogDebug(ai, "obra retomada: " + LabelOf(state) + " (peça " + (state.Index + 1) + "/"
				+ (state.Plan != null ? state.Plan.Count : 1) + ")");
			if (state.Group != null && !MHR_BuildGroup.TryClaim(state.Group, state.Index, ai.GetInstanceID()))
			{
				// the piece is another worker's now (the pause outlived the claim): take the next one of the site
				Advance(ai, state);
			}
			return;
		}
		Pending pending;
		if (s_pending.TryGetValue(ai.GetInstanceID(), out pending) && pending != null && pending.Paused)
		{
			pending.Paused = false;
			// the pause must not be counted as gathering time: the 300 s clock and the "still gathering" grace restart
			// with the resume (a pause of ten minutes would otherwise time the order out on the next frame)
			pending.Start = Time.time;
			pending.LastProgressAt = Time.time;
			pending.IdleSince = 0f;
			pending.NextCheck = 0f;
			ResumePending(ai, cs, pending);
		}
	}

	/// <summary>
	/// 11/11/2026 (the build panel): parks an order that is only <b>waiting for materials</b>. It stays in
	/// <c>s_pending</c> (the plan, the anchor, the piece and the retry queue travel with it) but the watcher ignores it
	/// until <see cref="Resume"/>, and the companion goes back to Follow with no gather cap and no work mode - the gather
	/// it was handed to is over as far as the order is concerned. <c>DropPending</c> cannot be reused: it never restored
	/// the mode, which is why the companion kept chopping wood with nothing to do.
	/// </summary>
	private static void ParkPending(CompanionAI ai, CompanionSetup cs, Pending pending)
	{
		if (pending == null)
		{
			return;
		}
		if (pending.Paused)
		{
			return;   // already parked
		}
		pending.Paused = true;
		RestoreFollow(ai, cs, pending);
		CompanionSetup.ClearOrderCap(cs);
		CompanionSetup.ApplyActionMode(cs, CompanionSetup.ModeFollow, false);
		LogDebug(ai, "obra pausada enquanto espera material: " + (pending.Template ?? pending.Blueprint ?? pending.Prefab));
	}

	/// <summary>11/11/2026 (the build panel): the order waiting for materials of this body is paused (the watcher must
	/// not touch it).</summary>
	private static bool PendingPaused(CompanionAI ai)
	{
		if (ai == null)
		{
			return false;
		}
		Pending pending;
		return s_pending.TryGetValue(ai.GetInstanceID(), out pending) && pending != null && pending.Paused;
	}

	/// <summary>11/11/2026 (the build panel): this order does not move right now - paused itself or a worker of a paused
	/// site. The site's flag is the one that holds when only the panel paused it.</summary>
	private static bool IsPaused(State state)
	{
		return state != null && (state.Paused || (state.Group != null && MHR_BuildGroup.Paused(state.Group)));
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
		// P1 (multi-build): the dead worker leaves the group (its piece is free for the others right away) - the group
		// itself goes on, and the pending hands the order back to this companion when it respawns
		if (state.Group != null)
		{
			MHR_BuildGroup.ReleaseAllOf(state.Group, ai.GetInstanceID());
			MHR_BuildGroup.Leave(state.Group, ai.GetInstanceID());
			state.Group = null;
		}
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
			Group = state.Group,
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

	/// <summary>
	/// P1 (multi-build, 03/11/2026): a resumed order (after a death or after the gather) goes back to its <b>group</b>
	/// when the site is still alive. The plan becomes the group's again (same list, same indices), the companion joins as
	/// a worker (the leader is the one that started the site) and the piece it was on is claimed again: when another
	/// worker holds it, or when it was <b>already built</b> while this one was away, it moves on to the next one.
	/// </summary>
	private static void RejoinGroup(CompanionAI ai, State state, string key)
	{
		if (ai == null || state == null || string.IsNullOrEmpty(key) || !MHR_BuildGroup.Exists(key))
		{
			return;
		}
		state.Group = key;
		List<PlannedStep> plan = MHR_BuildGroup.Plan(key);
		if (plan != null && plan.Count > 0)
		{
			state.Plan = plan;
		}
		MHR_BuildGroup.TryJoin(key, ai.GetInstanceID());
		// P1 (review A2): the leader is the SITE's - a companion that comes back is a normal worker again (the group
		// promoted another one when it left; Leave keeps a leader while anybody is on the site)
		if (!MHR_BuildGroup.BuildPhase(key))
		{
			// the site is still being leveled/cleared: it waits like any follower
			state.Phase = Phase.WaitGroup;
			state.Start = Time.time;
			ai.StopMoving();
			MHR_Mover.Release(ai);
			Log(ai, "voltou para o grupo \"" + MHR_BuildGroup.Label(key) + "\" - ainda esperando o canteiro");
			return;
		}
		if (state.Plan == null || state.Index < 0 || state.Index >= state.Plan.Count)
		{
			return;
		}
		if (MHR_BuildGroup.IsDone(key, state.Index))
		{
			// P1 (review B1, 03/11/2026): the piece it was on was built by the group while it was away - the pose is not
			// even asked (the site's own record is the truth), and Advance skips the rest of the tail it already built
			Log(ai, "a peça " + (state.Index + 1) + "/" + state.Plan.Count
				+ " já foi construída pelo grupo - seguindo para a próxima");
			Advance(ai, state);
			return;
		}
		PlannedStep step = state.Plan[state.Index];
		if (step != null)
		{
			state.Prefab = step.Prefab;
			state.Piece = step.Piece;
			state.Pos = step.Pos;
			state.Rot = step.Rot;
		}
		bool mine = MHR_BuildGroup.TryClaim(key, state.Index, ai.GetInstanceID());
		bool standing = false;
		if (mine && step != null && step.Piece != null)
		{
			// another worker may have built this very piece while this one was dead (or gathering). P1 (review A6): the
			// pose AND the orientation are asked - a neighbouring piece of the same prefab is not this one
			GameObject found;
			long creator = BuildExecutor.CreatorOf(ai.GetComponent<CompanionSetup>(), Player.m_localPlayer);
			standing = BuildExecutor.VerifyAt(step.Piece, step.Pos, step.Rot, creator, out found) == null;
		}
		if (!mine || standing)
		{
			if (standing)
			{
				CountPlaced(state);   // P1 (review A7): the group's index is marked done - counted once
				Log(ai, "a peça que ele estava fazendo já foi construída por outro companheiro - seguindo para a próxima");
			}
			else
			{
				Log(ai, "a peça que ele estava fazendo agora é de outro companheiro - seguindo para a próxima");
			}
			Advance(ai, state);
			return;
		}
		Log(ai, "voltou para o grupo \"" + MHR_BuildGroup.Label(key) + "\" na peça " + (state.Index + 1) + "/"
			+ state.Plan.Count);
	}

	/// <summary>
	/// 11/11/2026 (the build panel): one <b>site</b> as the panel shows it - the numbers of the order (plan, placed,
	/// skipped, emergency posts), its phase as a short token and who is on it. A plain snapshot: the panel asks for it
	/// about twice a second and must never steer anything (the panel's actions are <see cref="PauseSite"/>,
	/// <see cref="ResumeSite"/> and <see cref="CancelSite"/>).
	/// </summary>
	internal sealed class SiteInfo
	{
		/// <summary>The key of the group (<c>MHR_BuildGroup</c>), or null for a single worker / a pending order.</summary>
		internal string Key;
		/// <summary>What is being built (the blueprint's label, the template's, or the piece's name).</summary>
		internal string Label;
		/// <summary><c>paused</c>, <c>preparing</c>, <c>collecting</c>, <c>building</c>, <c>retrying</c> or <c>done</c>.</summary>
		internal string Phase;
		internal bool IsGroup;
		internal bool Paused;
		/// <summary>How many pieces the plan has.</summary>
		internal int Plan;
		/// <summary>How many of them are up (a set of plan indices - a piece is one piece, whatever it took).</summary>
		internal int Placed;
		/// <summary>How many were given up.</summary>
		internal int Skipped;
		/// <summary>How many emergency posts the site planted (<c>State.FallbackPosts</c>, summed over the workers).</summary>
		internal int Posts;
		internal int Workers;
		/// <summary>When the site started (the group's creation, the order's clock or the pending's handover).</summary>
		internal float StartedAt;
		/// <summary>Where the site is - the anchor the chests are searched around and the distance is measured from.</summary>
		internal Vector3 Anchor;
		/// <summary>Who is on it, in the site's own order (the leader is flagged).</summary>
		internal List<SiteWorker> Members;
		/// <summary>
		/// The very plan the site walks (the same list the group/state holds - not a copy). The panel does not need it;
		/// <see cref="SiteMaterials"/> does, so it can count the need without a second lookup that could disagree.
		/// </summary>
		internal List<PlannedStep> Steps;
	}

	/// <summary>11/11/2026 (the build panel): one companion of a site - the piece of the plan it is on is not exposed,
	/// the phase token is enough for the list.</summary>
	internal sealed class SiteWorker
	{
		internal int Id;
		internal string Name;
		internal bool Leader;
		internal bool Paused;
		internal string Phase;
	}

	/// <summary>
	/// 11/11/2026 (the build panel): the site of a player, if there is one - the group of the owner first (one site per
	/// player, <c>MHR_BuildGroup.SiteBusy</c>), then a single worker whose order has a plan (a template or a blueprint
	/// with nobody else), then an order that is only <b>waiting for materials</b> (its state is gone and the watcher
	/// carries the plan). False when the player has no build order at all.
	/// </summary>
	internal static bool CurrentSite(long owner, out SiteInfo site)
	{
		site = null;
		List<string> keys = MHR_BuildGroup.KeysOf(owner);
		for (int i = 0; i < keys.Count; i++)
		{
			SiteInfo group = GroupSite(keys[i]);
			if (group != null)
			{
				site = group;
				return true;
			}
		}
		foreach (KeyValuePair<int, State> kv in s_state)
		{
			State state = kv.Value;
			if (state == null || !state.Active || state.Group != null || state.Plan == null || state.Plan.Count == 0)
			{
				continue;
			}
			if (!IsOwner(kv.Key, owner))
			{
				continue;
			}
			site = StateSite(kv.Key, state);
			return true;
		}
		foreach (KeyValuePair<int, Pending> kv in s_pending)
		{
			Pending pending = kv.Value;
			if (pending == null || pending.Plan == null || pending.Plan.Count == 0)
			{
				continue;
			}
			if (pending.Group != null && MHR_BuildGroup.Exists(pending.Group))
			{
				continue;   // the site's group answers for it (it is one of the workers gathering for that site)
			}
			if (!IsOwner(kv.Key, owner))
			{
				continue;
			}
			site = PendingSite(kv.Key, pending);
			return true;
		}
		return false;
	}

	/// <summary>
	/// 11/11/2026 (the build panel): what the site <b>needs</b>, what it <b>has</b> and what is <b>missing</b>.
	/// <para>The need is the whole plan (<see cref="BuildMaterials"/> over the pieces the site walks - the same costs
	/// <c>BuildStock</c> pays per piece). The stock is every bag of the site <b>once</b> plus the usable chests around
	/// the anchor <b>once</b> (<c>BuildStock.Available</c>), never the same chest once per worker.</para>
	/// <para>Not called without a site (<see cref="CurrentSite"/> answers that first).</para>
	/// </summary>
	internal static void SiteMaterials(SiteInfo site, out Dictionary<string, int> need, out Dictionary<string, int> have,
		out Dictionary<string, int> missing)
	{
		need = new Dictionary<string, int>();
		have = new Dictionary<string, int>();
		missing = new Dictionary<string, int>();
		if (site == null)
		{
			return;
		}
		Dictionary<string, int> pieces = new Dictionary<string, int>();
		if (site.Steps != null)
		{
			for (int i = 0; i < site.Steps.Count; i++)
			{
				PlannedStep step = site.Steps[i];
				if (step == null || string.IsNullOrEmpty(step.Prefab))
				{
					continue;
				}
				int seen;
				pieces.TryGetValue(step.Prefab, out seen);
				pieces[step.Prefab] = seen + 1;
			}
		}
		Dictionary<string, Piece> cache = new Dictionary<string, Piece>(StringComparer.Ordinal);
		int noRecipe;
		need = BuildMaterials.Total(pieces, delegate(string prefab) { return CostsOfPiece(prefab, cache); }, out noRecipe);
		List<Inventory> bags = new List<Inventory>();
		Component who = null;
		if (site.Members != null)
		{
			for (int i = 0; i < site.Members.Count; i++)
			{
				CompanionSetup cs = SetupOf(site.Members[i].Id);
				if (cs == null)
				{
					continue;
				}
				if (who == null || site.Members[i].Leader)
				{
					who = cs;   // the leader answers for the site (the chest privacy is the owner's)
				}
				Humanoid humanoid = cs.GetComponent<Humanoid>();
				if (humanoid != null)
				{
					bags.Add(humanoid.GetInventory());
				}
			}
		}
		bool useChests = ModConfig.BuildUseChests == null || ModConfig.BuildUseChests.Value;
		have = BuildStock.Available(bags, site.Anchor, who, useChests);
		foreach (KeyValuePair<string, int> kv in need)
		{
			int got;
			have.TryGetValue(kv.Key, out got);
			if (got < kv.Value)
			{
				missing[kv.Key] = kv.Value - got;
			}
		}
	}

	/// <summary>11/11/2026 (the build panel): pauses the whole site - the group's flag and every worker on it.</summary>
	internal static void PauseSite(SiteInfo site)
	{
		if (site == null)
		{
			return;
		}
		if (site.IsGroup && !string.IsNullOrEmpty(site.Key))
		{
			MHR_BuildGroup.SetPaused(site.Key, true);
		}
		int count = PauseMembers(site);
		// a worker that handed the order to the gather is NOT in the group any more (Finish -> Leave) and only its
		// pending carries the site: it is parked here too, or the site would keep chopping while it is "paused"
		count += WalkSitePendings(site, false);
		LogSite("obra pausada: " + (site.Label ?? "?") + " (" + count + " companheiro(s))");
	}

	/// <summary>11/11/2026 (the build panel): resumes the whole site (the group's flag first, then every worker).</summary>
	internal static void ResumeSite(SiteInfo site)
	{
		if (site == null)
		{
			return;
		}
		if (site.IsGroup && !string.IsNullOrEmpty(site.Key))
		{
			MHR_BuildGroup.SetPaused(site.Key, false);
		}
		int count = 0;
		if (site.Members != null)
		{
			for (int i = 0; i < site.Members.Count; i++)
			{
				CompanionSetup cs = SetupOf(site.Members[i].Id);
				if (cs == null)
				{
					continue;
				}
				Resume(cs);
				count++;
			}
		}
		count += WalkSitePendings(site, true);
		LogSite("obra retomada: " + (site.Label ?? "?") + (count > 0 ? " (" + count + " companheiro(s))" : string.Empty));
	}

	/// <summary>
	/// 11/11/2026 (the build panel): "para" for the whole site - the same <see cref="Cancel"/> of the chat order for
	/// every worker (the group is dropped when the last one leaves and the pending orders fall with it).
	/// </summary>
	internal static void CancelSite(SiteInfo site)
	{
		if (site == null)
		{
			return;
		}
		if (site.IsGroup && !string.IsNullOrEmpty(site.Key))
		{
			MHR_BuildGroup.SetPaused(site.Key, false);
		}
		if (site.Members != null)
		{
			for (int i = 0; i < site.Members.Count; i++)
			{
				CompanionSetup cs = SetupOf(site.Members[i].Id);
				if (cs != null)
				{
					Cancel(cs);
				}
			}
		}
		// the workers that are gathering for this site are not in the group any more - their pending is what would
		// bring the build back (the very bug "para" had, fixed in Cancel), so it falls here too
		WalkSitePendings(site, false, true);
		LogSite("obra cancelada pelo painel: " + (site.Label ?? "?"));
	}

	/// <summary>
	/// 11/11/2026 (the build panel): the pending orders that belong to this site (a worker of it that is gathering) -
	/// parked (<c>resume: false</c>), resumed, or dropped (<c>cancel: true</c>). Returns how many it reached.
	/// </summary>
	private static int WalkSitePendings(SiteInfo site, bool resume, bool cancel = false)
	{
		if (site == null || !site.IsGroup || string.IsNullOrEmpty(site.Key))
		{
			return 0;   // a single worker's pending IS the site (it is already one of the members)
		}
		List<int> ids = new List<int>();
		foreach (KeyValuePair<int, Pending> kv in s_pending)
		{
			if (kv.Value != null && kv.Value.Group == site.Key)
			{
				ids.Add(kv.Key);
			}
		}
		int count = 0;
		for (int i = 0; i < ids.Count; i++)
		{
			CompanionSetup cs = SetupOf(ids[i]);
			if (cs == null)
			{
				continue;
			}
			if (cancel)
			{
				Cancel(cs);
			}
			else if (resume)
			{
				Resume(cs);
			}
			else
			{
				Pause(cs);
			}
			count++;
		}
		return count;
	}

	private static int PauseMembers(SiteInfo site)
	{
		int count = 0;
		if (site == null || site.Members == null)
		{
			return count;
		}
		for (int i = 0; i < site.Members.Count; i++)
		{
			CompanionSetup cs = SetupOf(site.Members[i].Id);
			if (cs == null)
			{
				continue;
			}
			Pause(cs);
			count++;
		}
		return count;
	}

	/// <summary>The site of a group (<c>MHR_BuildGroup</c>): its plan, its counters and its workers.</summary>
	private static SiteInfo GroupSite(string key)
	{
		if (!MHR_BuildGroup.Exists(key))
		{
			return null;
		}
		List<PlannedStep> plan = MHR_BuildGroup.Plan(key);
		List<int> workers = MHR_BuildGroup.WorkerIds(key);
		bool buildPhase = MHR_BuildGroup.BuildPhase(key);
		int leaderId = MHR_BuildGroup.LeaderId(key);
		SiteInfo site = new SiteInfo
		{
			Key = key,
			Label = MHR_BuildGroup.Label(key),
			IsGroup = true,
			Paused = MHR_BuildGroup.Paused(key),
			StartedAt = MHR_BuildGroup.CreatedAt(key),
			Steps = plan,
			Plan = plan != null ? plan.Count : 0,
			Placed = MHR_BuildGroup.Placed(key),
			Skipped = MHR_BuildGroup.Skipped(key),
			Workers = workers.Count,
			Members = new List<SiteWorker>(workers.Count)
		};
		bool preparing = false;
		bool collecting = false;
		bool building = false;
		bool retrying = false;
		for (int i = 0; i < workers.Count; i++)
		{
			int id = workers[i];
			State state;
			s_state.TryGetValue(id, out state);
			if (state != null)
			{
				site.Posts += state.FallbackPosts.Count;
			}
			SiteWorker member = new SiteWorker
			{
				Id = id,
				Name = NameOfWorker(id),
				Leader = id == leaderId,
				Paused = state != null && state.Paused,
				Phase = PhaseToken(state, buildPhase)
			};
			site.Members.Add(member);
			if (member.Phase == "preparing")
			{
				preparing = true;
			}
			else if (member.Phase == "collecting")
			{
				collecting = true;
			}
			else if (member.Phase == "building")
			{
				building = true;
			}
			else if (member.Phase == "retrying")
			{
				retrying = true;
			}
		}
		Vector3 anchor;
		if (SiteAnchor(leaderId, workers, out anchor))
		{
			site.Anchor = anchor;
		}
		else
		{
			// nobody of the site holds a state right now (they are all gathering): the pending knows the anchor
			site.Anchor = GroupAnchor(key);
		}
		// the site's phase: what it is doing as a whole (the priority the panel colors by)
		if (site.Paused)
		{
			site.Phase = "paused";
		}
		else if (preparing)
		{
			site.Phase = "preparing";
		}
		else if (collecting || GroupCollecting(key))
		{
			site.Phase = "collecting";
		}
		else if (building)
		{
			site.Phase = "building";
		}
		else if (retrying)
		{
			site.Phase = "retrying";
		}
		else
		{
			site.Phase = "done";
		}
		return site;
	}

	/// <summary>The site of a single worker (a template/blueprint order with no group): its own state is the site.</summary>
	private static SiteInfo StateSite(int workerId, State state)
	{
		SiteInfo site = new SiteInfo
		{
			Key = null,
			Label = state.Blueprint ?? (state.Template != null ? state.Template.Label : LabelOf(state)),
			IsGroup = false,
			Paused = state.Paused,
			StartedAt = state.Start,
			Steps = state.Plan,
			Plan = state.Plan != null ? state.Plan.Count : 0,
			Placed = state.PlacedIndices.Count,
			Skipped = state.SkippedIndices.Count,
			Posts = state.FallbackPosts.Count,
			Workers = 1,
			Anchor = state.AnchorKnown ? state.Anchor : state.Hint,
			Members = new List<SiteWorker>(1)
		};
		site.Phase = site.Paused ? "paused" : PhaseToken(state, true);
		site.Members.Add(new SiteWorker
		{
			Id = workerId,
			Name = NameOfWorker(workerId),
			Leader = true,
			Paused = site.Paused,
			Phase = site.Phase
		});
		return site;
	}

	/// <summary>The site of an order waiting for its materials (the state is gone, the plan travels with the pending).</summary>
	private static SiteInfo PendingSite(int workerId, Pending pending)
	{
		SiteInfo site = new SiteInfo
		{
			Key = null,
			Label = pending.Template ?? pending.Blueprint ?? pending.Prefab,
			IsGroup = false,
			Paused = pending.Paused,
			StartedAt = pending.Start,
			Steps = pending.Plan,
			Plan = pending.Plan != null ? pending.Plan.Count : 0,
			Workers = 1,
			Anchor = pending.Anchor,
			Members = new List<SiteWorker>(1)
		};
		site.Phase = pending.Paused ? "paused" : "collecting";
		site.Members.Add(new SiteWorker
		{
			Id = workerId,
			Name = NameOfWorker(workerId),
			Leader = true,
			Paused = site.Paused,
			Phase = site.Phase
		});
		return site;
	}

	/// <summary>
	/// The short token of what one worker is doing (the panel's list). The priority is the site's
	/// (<see cref="GroupSite"/>): <c>preparing</c> (leveling, clearing, or a follower waiting for the site) &lt;
	/// <c>collecting</c> (walking to the resource, or the gather that took the order over) &lt; <c>building</c> &lt;
	/// <c>retrying</c> (the retry queue) &lt; <c>done</c>. A null state is a worker with nothing to show yet.
	/// </summary>
	private static string PhaseToken(State state, bool buildPhase)
	{
		if (state == null)
		{
			return "waiting";
		}
		switch (state.Phase)
		{
			case Phase.Level:
			case Phase.Clear:
				return "preparing";
			case Phase.WaitGroup:
				// a follower holds still while the leader works the site; with the site open it is about to build
				return buildPhase ? "building" : "preparing";
			case Phase.GatherWalk:
				return "collecting";
			case Phase.WaitRetry:
				return "retrying";
			case Phase.Find:
			case Phase.Goto:
			case Phase.Place:
			case Phase.BodyRetry:
			case Phase.Verify:
				return "building";
			default:
				return "done";
		}
	}

	/// <summary>The anchor of the site: the leader's pose, else any worker's that knows it (the leader may be away).</summary>
	private static bool SiteAnchor(int leaderId, List<int> workers, out Vector3 anchor)
	{
		anchor = Vector3.zero;
		State lead;
		if (leaderId != 0 && s_state.TryGetValue(leaderId, out lead) && lead != null && lead.AnchorKnown)
		{
			anchor = lead.Anchor;
			return true;
		}
		if (workers == null)
		{
			return false;
		}
		for (int i = 0; i < workers.Count; i++)
		{
			State state;
			if (s_state.TryGetValue(workers[i], out state) && state != null && state.AnchorKnown)
			{
				anchor = state.Anchor;
				return true;
			}
		}
		return false;
	}

	/// <summary>The anchor a pending of this site carries (no worker of it holds a state right now).</summary>
	private static Vector3 GroupAnchor(string key)
	{
		if (!string.IsNullOrEmpty(key))
		{
			foreach (KeyValuePair<int, Pending> kv in s_pending)
			{
				if (kv.Value != null && kv.Value.Group == key)
				{
					return kv.Value.Anchor;
				}
			}
		}
		return Vector3.zero;
	}

	/// <summary>Is one of the site's workers collecting for it right now (its order is a pending with this group)?</summary>
	private static bool GroupCollecting(string key)
	{
		if (string.IsNullOrEmpty(key))
		{
			return false;
		}
		foreach (KeyValuePair<int, Pending> kv in s_pending)
		{
			if (kv.Value != null && kv.Value.Group == key && !kv.Value.Paused)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>The companion of one worker id (the states and the group's workers are keyed by the CompanionAI's
	/// instance id; the setup is what has a name and an owner).</summary>
	private static CompanionSetup SetupOf(int workerId)
	{
		IReadOnlyList<CompanionSetup> all = CompanionSetup.AllCompanions;
		for (int i = 0; i < all.Count; i++)
		{
			CompanionSetup cs = all[i];
			CompanionAI ai = cs != null ? cs.GetComponent<CompanionAI>() : null;
			if (ai != null && ai.GetInstanceID() == workerId)
			{
				return cs;
			}
		}
		return null;
	}

	private static string NameOfWorker(int workerId)
	{
		CompanionSetup cs = SetupOf(workerId);
		return cs != null ? CompanionNames.NameOf(cs) : null;
	}

	/// <summary>The worker belongs to that player (the same rule the orders use: an unclaimed companion is the local
	/// player's, see <c>MHR_Owner.CreatorId</c>).</summary>
	private static bool IsOwner(int workerId, long owner)
	{
		CompanionSetup cs = SetupOf(workerId);
		return cs != null && MHR_Owner.CreatorId(cs) == owner;
	}

	/// <summary>The costs of one prefab of the plan (resolved once per call - the panel asks about twice a second).</summary>
	private static IReadOnlyList<Cost> CostsOfPiece(string prefab, Dictionary<string, Piece> cache)
	{
		Piece piece;
		if (cache == null || !cache.TryGetValue(prefab, out piece))
		{
			piece = BuildProbe.PieceOf(prefab);
			if (cache != null)
			{
				cache[prefab] = piece;
			}
		}
		return BuildStock.CostsOf(piece);
	}

	/// <summary>11/11/2026 (the build panel): one line per site action, without a companion's name (the site is the
	/// subject, not the worker).</summary>
	private static void LogSite(string message)
	{
		if (CompanionsPlugin.LogNormal && CompanionsPlugin.Log != null)
		{
			CompanionsPlugin.Log.LogInfo(LogTag + message);
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
			// E4b: nothing to drive, but a build may be waiting for the gather to finish.
			// 11/11/2026 (the build panel): a PAUSED pending is not touched by the watcher at all - no resume when the
			// bag is covered, no gather timeout: it waits for the panel (or for "para").
			if (!PendingPaused(ai))
			{
				CheckPending(ai);
			}
			return false;
		}
		// 11/11/2026 (the build panel): a paused order (or a worker of a paused SITE) is alive but does not move.
		// LastDrive is NOT touched here on purpose: the shift right below discounts the pause from the order's own
		// clock when it resumes (that is what keeps the Timeout honest).
		if (IsPaused(state))
		{
			return true;
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
		else if (state.Phase != Phase.GatherWalk && state.Phase != Phase.Clear && state.Phase != Phase.WaitGroup
			&& state.Phase != Phase.WaitRetry && Time.time - state.Start > Timeout)
		{
			// E7: the clearing has its own budget (ClearBudget) - a 60 s clock would end the order on the second tree
			// P1: a follower WAITING for the leader's site (or for the group's retry queue) has no clock at all
			why = "the order took too long";
		}
		// E6 (measured 01/11/2026): an encumbered companion cannot walk - the game's Encumbered effect slows it to a
		// crawl and MHR_Mover gives up after 8 s without progress, which used to end the order with "no site" (a lie).
		// BUG do teste do jogador (03/11/2026): CompanionCarry.Fraction faz Mathf.Clamp01, entao "Fraction > 1" NUNCA era
		// verdade - a fala hc_ai_build_too_heavy era codigo morto e o companheiro tentava andar carregado (o mover
		// desistia em 8 s e a ordem dizia "can't get to the spot", uma mentira). Compara o peso com o teto de verdade.
		// E7: while CLEARING the site the baggage is not an error - the wood of the chopped trees lands in the same bag
		// the build pays from, and the check fires on the first building frame anyway (with the honest numbers).
		if (why == null && state.Phase != Phase.Clear && state.Phase != Phase.WaitGroup && state.Phase != Phase.WaitRetry
			&& humanoid != null
			&& humanoid.GetInventory() != null
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
			SkipPiece(ai, state, "took too long");
			return Advance(ai, state);
		}
		if (why != null)
		{
			Finish(ai, state, "hc_ai_build_failed", why, LabelOf(state));
			return false;
		}

		// P1 (multi-build, 03/11/2026): the <b>claims</b>. A live worker keeps the piece it is on (the 20 s expiry is for
		// a worker whose game went away, not for a long walk), and the LEADER is the one that opens the build phase for
		// the others once the site is ready - taking its own piece at that very moment, so the followers do not start on
		// the same one. P1 (review A2): the leader is asked of the GROUP (it is reassigned when the old one leaves).
		if (state.Group != null && state.Phase != Phase.WaitGroup)
		{
			MHR_BuildGroup.Refresh(state.Group, state.Index, ai.GetInstanceID());
			if (!MHR_BuildGroup.BuildPhase(state.Group) && IsLeader(ai, state)
				&& state.Phase != Phase.Clear && state.Phase != Phase.Level)
			{
				MHR_BuildGroup.SetBuildPhase(state.Group, true);
				Log(ai, "o canteiro está pronto - o grupo pode construir");
				if (!MHR_BuildGroup.TryClaim(state.Group, state.Index, ai.GetInstanceID()))
				{
					return Advance(ai, state);
				}
			}
		}

		switch (state.Phase)
		{
			case Phase.Level:
				return LevelGround(ai, state);
			case Phase.Clear:
				return ClearSite(ai, state, dt);
			case Phase.WaitGroup:
				return WaitGroup(ai, state);
			case Phase.WaitRetry:
				return WaitRetry(ai, state, dt);
			case Phase.Find:
				return state.Template != null ? FindPlan(ai, state) : Find(ai, state);
			case Phase.Goto:
				return Goto(ai, state, cs, dt);
			case Phase.Place:
				return Place(ai, state, cs, bag);
			case Phase.BodyRetry:
				return BodyRetry(ai, state);
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
	/// P1 (multi-build, 03/11/2026): a <b>follower</b> waits while the leader levels and clears the site. The phase does
	/// nothing and does not end the order (no mover, no clock, no cancellation) - holding still is the whole job, and the
	/// idle job does not take the body because the order is alive. When the leader opens
	/// <see cref="MHR_BuildGroup.BuildPhase"/>, the follower takes a piece nobody else has claimed and starts walking.
	/// </summary>
	private static bool WaitGroup(CompanionAI ai, State state)
	{
		if (state.Group == null)
		{
			state.Phase = Phase.Goto;   // no group (it was dropped): just build what is in the plan
			return true;
		}
		if (!MHR_BuildGroup.Exists(state.Group))
		{
			Log(ai, "o grupo da obra não existe mais - a ordem termina");
			Finish(ai, state, null, "o grupo não existe mais");
			return false;
		}
		if (!MHR_BuildGroup.BuildPhase(state.Group))
		{
			return true;   // the leader is still working the ground/site
		}
		state.Start = Time.time;
		state.Checked = false;
		state.FallDrops = 0;
		state.BestDistance = 0f;
		state.BestAt = Time.time;
		if (state.Plan == null || state.Plan.Count == 0)
		{
			Log(ai, "o plano do grupo está vazio - a ordem termina");
			Finish(ai, state, null, "plano vazio");
			return false;
		}
		int worker = ai.GetInstanceID();
		// P1 (review B1, 03/11/2026): the piece the follower was going to start may have been built by the leader while it
		// waited - a done index is skipped (Advance walks from the next one), it is never claimed and never walked to
		if (state.Index >= 0 && state.Index < state.Plan.Count && !MHR_BuildGroup.IsDone(state.Group, state.Index)
			&& MHR_BuildGroup.TryClaim(state.Group, state.Index, worker))
		{
			EnterStep(state, state.Index);
			Log(ai, "o canteiro está pronto - construindo " + state.Prefab + " (" + (state.Index + 1) + "/"
				+ state.Plan.Count + ")");
			return true;
		}
		Log(ai, "o canteiro está pronto - procurando uma peça que ninguém pegou");
		return Advance(ai, state);
	}

	/// <summary>
	/// P1 (review A1, 03/11/2026): the worker finished the plan and the site still has pieces in its <b>retry queue</b>
	/// (they did not stand yet, and they may be another worker's or not due yet). It waits here instead of ending the
	/// order: the piece is pulled the moment it is free (<see cref="MHR_BuildGroup.NextRetry"/>). When nothing is left the
	/// group's end runs (<see cref="Advance"/>).
	///
	/// <para>P1 (04/11/2026, the <b>stall watchdog</b>): the wait is bounded by the SITE's progress, not by a clock that
	/// starts when this worker happens to wait - if no piece went up in <see cref="StallSeconds"/>, the pieces still in the
	/// queue need a support that is not coming: they are given up (counted as skipped, named in the log) and the order ends
	/// through the normal end. A single worker (no group) waits on its own <see cref="State.RetryAt"/> and is watched the
	/// same way.</para>
	///
	/// <para>11/11/2026 (Run 10): the wait is also where a companion of the site that is <b>in the way</b> of a piece another
	/// one has to place steps aside (<see cref="StepAside"/>, <c>[Building] StepAside</c>) - a step of 2,2 m through the
	/// mover, never a teleport and never the player.</para>
	/// </summary>
	private static bool WaitRetry(CompanionAI ai, State state, float dt)
	{
		// 11/11/2026 (Run 10, the companion blocker): a companion that is only WAITING steps out of the way of a piece
		// another one of the site has to place. It is a step of 2,2 m through the mover (never a teleport), it is answered
		// here and nowhere else (Goto/Place/Clear/Gather never move somebody else's piece out of the way) and it expires -
		// a worker that is not waiting never consumes the request and nobody is moved for a piece that is no longer coming.
		if (StepAside(ai, state, dt))
		{
			state.WaitAside += dt;   // 11/11/2026 (Run 17): the step aside is a wait of the order too
			return true;
		}
		float progress = state.Group != null ? MHR_BuildGroup.LastProgressAt(state.Group) : state.LastProgressAt;
		if (progress <= 0f)
		{
			// a resumed/just-started order: the clock starts now (nothing has failed for lack of support yet)
			progress = Time.time;
			state.LastProgressAt = progress;
		}
		if (Time.time - progress > StallSeconds)
		{
			return StallOut(ai, state);
		}
		// 11/11/2026 (the storey barrier): while the plan waits below a storey, the pieces of the queue BELOW it are the only
		// ones served - the normal NextRetry would happily take a piece of the storey above and climb over the entrance the
		// player asked to be settled first. Nothing below anymore (or patience over) drops the barrier and the normal path
		// goes on.
		if (ServeBarrier(ai, state, dt))
		{
			return true;
		}
		if (state.Group == null)
		{
			// the single worker's own retry list: wait for the dependency clock, then let Advance append it again
			if (state.Plan == null || state.Retry == null || state.Retry.Count == 0)
			{
				return Advance(ai, state);
			}
			if (Time.time < state.RetryAt)
			{
				state.WaitQueueEnd += dt;   // 11/11/2026 (Run 17): the single worker's queue, counted like the group's
				return true;
			}
			return Advance(ai, state);
		}
		if (!MHR_BuildGroup.Exists(state.Group))
		{
			Log(ai, "o grupo da obra não existe mais - a ordem termina");
			Finish(ai, state, null, "o grupo não existe mais");
			return false;
		}
		if (state.Plan == null || state.Plan.Count == 0)
		{
			return Advance(ai, state);
		}
		bool spread;
		int retry = MHR_BuildGroup.NextRetry(state.Group, ai.GetInstanceID(), out spread);
		if (retry >= 0 && retry < state.Plan.Count)
		{
			if (spread)
			{
				state.SpreadResumed++;   // 11/11/2026 (the spread of the crew): a piece left for later came back
			}
			EnterStep(state, retry);
			Log(ai, "retry do grupo: " + state.Prefab + " (" + (retry + 1) + "/" + state.Plan.Count + ")");
			return true;
		}
		// 11/11/2026 (Run 18, the starvation of the walk): a worker that entered this wait in the MIDDLE of the plan (the
		// body gate at `:1151` or the storey barrier at `:1433`) used to never ask the gate again - the wait only walked the
		// plan when the site's queue was EMPTY (`:3620`), and the queue is fed by the spread on almost every piece. So it sat
		// out `entry.At` (2-25 s) with a free piece right ahead. When there is still a piece ahead of the walk, Advance is
		// the one that decides (it re-asks MayTake and the storey barrier, and the end-of-plan guard lives there too, `:1227`).
		bool ahead = state.Index + 1 < state.Plan.Count;
		if (ahead)
		{
			return Advance(ai, state, dt);
		}
		if (!MHR_BuildGroup.RetryPending(state.Group))
		{
			return Advance(ai, state);   // nothing left: the plan is done for everybody
		}
		// 11/11/2026 (Run 17): there IS something in the queue but this worker may not take it (other worker's, a body, the
		// spread gate) - it is waiting, and the summary line counts it as such.
		// 11/11/2026 (Run 18): the seconds are split by WHERE the walk is - at the END of the plan (nothing ahead, the queue
		// is the legitimate way forward) or in the MIDDLE of it (a piece was right ahead). With the `ahead` guard above this
		// middle number is ≈ 0 by construction; it stays here as the guard of a later edit - the day the early return is
		// removed or its condition changes, the middle-of-plan wait shows up again in the summary line instead of hiding.
		if (ahead)
		{
			state.WaitAhead += dt;
		}
		else
		{
			state.WaitQueueEnd += dt;
		}
		return true;
	}

	/// <summary>
	/// P1 (04/11/2026, the <b>stall watchdog</b>): nothing went up in <see cref="StallSeconds"/>, so the pieces still
	/// waiting in the queue need a support that will never be there (a hole, a floor that was never built). They are given
	/// up - counted once per plan index - the log names each one (PT-BR, one line per piece), and the order ends through
	/// <see cref="Advance"/> so the player hears the normal numbers.
	/// </summary>
	private static bool StallOut(CompanionAI ai, State state)
	{
		if (state.Group != null)
		{
			// 11/11/2026 (the body barrier): the watchdog's verdict is "this site is not moving" - a barrier that is only
			// WAITING for a body goes with the queue, or Advance would send the worker back to WaitRetry and the same
			// pieces would be given up again on the next frame.
			MHR_BuildGroup.ClearBodies(state.Group);
			List<int> pending = new List<int>();
			int dropped = MHR_BuildGroup.AbandonRetry(state.Group, ai.GetInstanceID(), pending);
			LogStall(ai, state, pending, dropped);
			return Advance(ai, state);
		}
		if (state.Plan == null || state.Retry == null || state.Retry.Count == 0)
		{
			return Advance(ai, state);
		}
		int single = 0;
		for (int i = state.Retry.Count - 1; i >= 0; i--)
		{
			PlannedStep step = state.Retry[i];
			state.Retry.RemoveAt(i);
			if (step == null)
			{
				continue;
			}
			int index = state.Plan.IndexOf(step);
			if (index < 0 || state.PlacedIndices.Contains(index))
			{
				continue;
			}
			if (state.SkippedIndices.Add(index))
			{
				single++;
			}
			MHR_BuildGroup.AddSkipped(state.Group, index);
			if (CompanionsPlugin.LogNormal)
			{
				Log(ai, "sem apoio: " + step.Prefab + " em (" + F(step.Pos.x) + ", " + F(step.Pos.y) + ", "
					+ F(step.Pos.z) + ")");
			}
		}
		if (CompanionsPlugin.LogNormal)
		{
			Log(ai, "sem progresso há " + F(StallSeconds) + " s - " + single + " peça(s) sem apoio ficam de fora");
		}
		return Advance(ai, state);
	}

	/// <summary>P1 (04/11/2026): the watchdog names the pieces it is leaving out (prefab and pose), so a test in the game
	/// says exactly what did not go up. One line per piece, inside the log gate.</summary>
	private static void LogStall(CompanionAI ai, State state, List<int> pending, int dropped)
	{
		if (!CompanionsPlugin.LogNormal)
		{
			return;
		}
		Log(ai, "sem progresso há " + F(StallSeconds) + " s - " + dropped + " peça(s) sem apoio ficam de fora");
		if (state.Plan == null || pending == null)
		{
			return;
		}
		for (int i = 0; i < pending.Count; i++)
		{
			int index = pending[i];
			if (index < 0 || index >= state.Plan.Count)
			{
				continue;
			}
			PlannedStep step = state.Plan[index];
			if (step == null)
			{
				continue;
			}
			Log(ai, "sem apoio: " + step.Prefab + " em (" + F(step.Pos.x) + ", " + F(step.Pos.y) + ", "
				+ F(step.Pos.z) + ")");
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

	/// <summary>
	/// Lote 1 (player's request, 11/11/2026): is the station rule still in force for this <b>kind</b> of order?
	/// <c>[Building] BlueprintStations</c> (default on) lifts it <b>only</b> for a blueprint - a template and a single
	/// piece keep the vanilla rule and the switch does not reach them (<see cref="BuildStationCover.Required"/>). The
	/// value is read on every use, so the F8 panel also changes an order that is already running.
	/// </summary>
	private static bool StationRequired(bool isBlueprint)
	{
		bool waived = ModConfig.BuildBlueprintStations == null ? BuildStationCover.Default : ModConfig.BuildBlueprintStations.Value;
		return BuildStationCover.Required(isBlueprint, waived);
	}

	/// <summary>The same question for the order being carried out (a template or a single piece is never waived).</summary>
	private static bool StationRequired(State state)
	{
		return StationRequired(state != null && state.Blueprint != null);
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
	///
	/// <para>P1 (04/11/2026, measured on the 985-piece house): the pass used to stop after 6 <b>searches</b>, so a 32 m
	/// site got 2 benches ~18,5 m from the centre and the middle of the house fell into <c>needs_station</c> in the middle
	/// of the build (and in a group the fallback appends a bench at the END of the plan, so those pieces waited for the
	/// whole house). Now the budget is per bench (up to <see cref="MaxPlannedBenches"/> planned,
	/// <see cref="MaxBenchSearches"/> searches) and the search only tries anchors <b>outside</b> the footprint (the piece's
	/// side, +-40 degrees and a farther ring), all of them standing on the terrain. The pass ends with an honest summary:
	/// how many pieces need a bench, how many were planned and which ones are left without coverage (prefab and pose).</para>
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
		int searches = 0;
		// the two ways a piece can be left without a station - the log says which one (a budget that ran out is not
		// "there is no room")
		List<PlannedStep> noRoom = new List<PlannedStep>();
		List<PlannedStep> budget = new List<PlannedStep>();
		for (int i = 0; i < plan.Count; i++)
		{
			PlannedStep step = plan[i];
			if (step.Piece == null || step.Piece.m_craftingStation == null || !IsBenchKind(step.Piece.m_craftingStation.m_name))
			{
				continue;
			}
			needed++;
			// a bench of the WORLD (the player's own, or one the plan already put up) covers it - not a search
			if (CraftingStation.HaveBuildStationInRange(step.Piece.m_craftingStation.m_name, step.Pos) != null)
			{
				continue;
			}
			// a bench this pass already planned covers it - not a search either (04/11/2026: the old limit counted these
			// and the measured house ran out of searches with 981 pieces still waiting)
			if (CoveredBy(planned, step.Pos, radius))
			{
				continue;
			}
			if (added >= MaxPlannedBenches || searches >= MaxBenchSearches)
			{
				budget.Add(step);
				continue;
			}
			searches++;
			Vector3 spot;
			Quaternion benchRot;
			// the bench must also be within its own radius of the piece it is being planned for (the check is inside: a
			// rotated or far anchor can find room where the bench would not serve this piece)
			if (!FindBenchSpotOutside(bench, step.Pos, centre, outside, rot, who, out spot, out benchRot, radius))
			{
				noRoom.Add(step);
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
			Log(ai, "station: " + needed + " peça(s) precisam, " + added + " planejada(s), " + (noRoom.Count + budget.Count)
				+ " sem cobertura");
		}
		if (CompanionsPlugin.LogNormal)
		{
			for (int i = 0; i < noRoom.Count; i++)
			{
				PlannedStep step = noRoom[i];
				Log(ai, "sem bancada: " + step.Prefab + " em (" + F(step.Pos.x) + ", " + F(step.Pos.y) + ", " + F(step.Pos.z)
					+ ") - nenhum lugar livre fora do footprint (âncora que colide é recusada)");
			}
			for (int i = 0; i < budget.Count; i++)
			{
				PlannedStep step = budget[i];
				Log(ai, "sem bancada: " + step.Prefab + " em (" + F(step.Pos.x) + ", " + F(step.Pos.y) + ", " + F(step.Pos.z)
					+ ") - orçamento de bancadas do plano esgotado (" + added + "/" + MaxPlannedBenches + " planejadas, "
					+ searches + "/" + MaxBenchSearches + " buscas)");
			}
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
		// P1 (multi-build): only the LEADER touches the plan's stations while more than one worker walks it - the plan is
		// the group's and inserting a step in the middle of it shifts the indices every other worker walks by (one bench
		// serves everybody: the game's rule is per piece). In a group the bench goes to the END of the plan instead: the
		// refused piece is retried after it, and nobody's index moves. P1 (review B1, 03/11/2026): this holds for a group
		// of ONE too - the site's indices (claims, `Done` and the retry queue) are the site's, not the worker's.
		bool shared = PlanLocked(state);
		bool append = state.Group != null;
		if (shared && !IsLeader(ai, state))
		{
			return false;
		}
		Piece bench = BuildProbe.PieceOf(BenchPrefab);
		if (bench == null)
		{
			return false;
		}
		Vector3 centre = state.AnchorKnown ? state.Anchor : state.Pos;
		float outside = Mathf.Max(state.ClearSize * 0.5f, 6f) + 2.5f;
		Vector3 spot;
		Quaternion benchRot;
		// P1 (04/11/2026): the same outside-only, on-the-ground search as the station pass - a bench at the piece's own pose
		// would take the place of a floor (rule of 03/11/2026)
		if (!FindBenchSpotOutside(bench, state.Pos, centre, outside, state.Rot, Self(ai), out spot, out benchRot))
		{
			Log(ai, "no room for a workbench near the " + refusedPrefab + " - the piece is only retried");
			return false;
		}
		if (append)
		{
			state.Plan.Add(new PlannedStep { Prefab = BenchPrefab, Piece = bench, Pos = spot, Rot = benchRot });
			state.BenchInserts++;
			Log(ai, "sem bancada no alcance da " + refusedPrefab + " - uma foi para o FIM do plano (o plano do grupo não é"
				+ " reordenado) em (" + F(spot.x) + ", " + F(spot.y) + ", " + F(spot.z) + ")");
			return false;   // the refused piece goes to the retry queue, which runs after the bench
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
	///
	/// <para>P1 (04/11/2026): <paramref name="onGround"/> makes the pose stand on the <b>terrain</b> no matter how far the
	/// piece's height is from it - an anchor outside the house is on the ground, and the old 3 m tolerance refused a bench
	/// on a slope (the ground of a 32 m site is metres below the floor). The ring of 1,6 m is walked once per direction and
	/// the anchor itself is one position, not eight (the old loop repeated it), so a failed search costs 36 checks instead
	/// of 64. The verdict of the accepted pose comes out in <paramref name="verdict"/> so the caller can refuse a pose the
	/// game calls <c>clipping</c> (see <see cref="TryBenchAt"/>).</para>
	/// </summary>
	private static bool FindBenchSpot(Piece bench, Vector3 near, Quaternion rot, Component who, out Vector3 spot,
		out Quaternion benchRot, out Dictionary<string, object> verdict, bool onGround = false)
	{
		verdict = null;
		// the OUTDOOR spot is on the ground (a bench outside the house stands on the terrain, not at the floor's height);
		// the fallback (the piece's own spot) keeps its height, for a bench that has to go inside
		float groundY = ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(near) : near.y;
		bool outdoors = onGround || Mathf.Abs(groundY - near.y) < 3f;
		float height = outdoors ? groundY : near.y;
		for (int dir = -1; dir < 8; dir++)
		{
			// dir -1 is the anchor itself (one position, whatever the yaw); 0..7 walk a ring of 1,6 m
			float distance = dir < 0 ? 0f : 1.6f;
			float angle = Mathf.PI * 2f * Mathf.Max(dir, 0) / 8f;
			Vector3 candidate = new Vector3(near.x + Mathf.Cos(angle) * distance, height, near.z + Mathf.Sin(angle) * distance);
			for (int yaw = 0; yaw < 4; yaw++)
			{
				Quaternion candidateRot = rot * Quaternion.Euler(0f, yaw * 90f, 0f);
				Dictionary<string, object> check = BuildSite.Check(bench, candidate, candidateRot, who);
				if (Ok(check))
				{
					spot = candidate;
					benchRot = candidateRot;
					verdict = check;
					return true;
				}
			}
		}
		spot = near;
		benchRot = rot;
		return false;
	}

	/// <summary>
	/// P1 (04/11/2026): a workbench pose <b>outside the plan's footprint</b>, on the side of the piece it will serve. The
	/// first anchor is the one the pass always used (the piece's direction, <paramref name="outside"/> metres from the
	/// centre); when it has no room, the same direction at a farther ring and +-40 degrees around it are tried before giving
	/// up. Every anchor is on the <b>terrain</b> (an outside bench stands on the ground, not at the house's floor height) and
	/// nothing is ever tried INSIDE the footprint: the bench would take the place of a floor (rule of 03/11/2026) - which is
	/// also why the rotated anchors sit on the FAR ring (a 40 degree turn at the near radius of a 32 m square lands inside
	/// it). <paramref name="cover"/> is the radius the bench has to serve: a spot that does not reach the piece is not
	/// accepted (a rotated or far anchor can find room where the bench is out of range).
	/// </summary>
	private static bool FindBenchSpotOutside(Piece bench, Vector3 piece, Vector3 centre, float outside, Quaternion rot,
		Component who, out Vector3 spot, out Quaternion benchRot, float cover = 0f)
	{
		Vector3 outward = piece - centre;
		outward.y = 0f;
		if (outward.sqrMagnitude < 1f)
		{
			outward = new Vector3(1f, 0f, 0f);
		}
		outward.Normalize();
		float near = Mathf.Max(outside, 2.5f);
		// the diagonal of a square is ~1,42x its half side: this ring is outside the footprint in ANY direction
		float far = Mathf.Max(near * 1.5f, near + 2f);
		if (TryBenchAt(bench, centre + outward * near, piece, rot, who, cover, out spot, out benchRot))
		{
			return true;
		}
		if (TryBenchAt(bench, centre + outward * far, piece, rot, who, cover, out spot, out benchRot))
		{
			return true;
		}
		for (int turn = -1; turn <= 1; turn += 2)
		{
			Vector3 direction = Quaternion.Euler(0f, turn * 40f, 0f) * outward;
			if (TryBenchAt(bench, centre + direction * far, piece, rot, who, cover, out spot, out benchRot))
			{
				return true;
			}
		}
		spot = piece;
		benchRot = rot;
		return false;
	}

	/// <summary>P1 (04/11/2026): one anchor of <see cref="FindBenchSpotOutside"/> - the pose search plus "does the bench
	/// really serve the piece" (<paramref name="cover"/> 0 = do not ask).
	///
	/// <para>P1 (04/11/2026, second run: the planned bench at (-178,3, 38,6, -431,7) was refused at placement with
	/// <c>clipping</c>): a pose the game's verdict calls <b>clipping</b> is never accepted here - the anchor is abandoned and
	/// the next one (farther out, or turned 40 degrees) is tried. <c>BuildSite.Check</c> runs the ghost test for a piece with
	/// <c>m_noClipping</c> (the workbench has it) and already clears <c>ok</c> when it clips; this is the <b>second barrier</b>,
	/// read from the verdict itself (the <c>clipping</c> flag and the status), so no anchoring rule can put a clipping pose in
	/// the plan even if the caller's <c>Ok</c> changes. The world can still change between the plan and the placement - that is
	/// what <c>MHR_Build.NudgePiece</c> handles.</para>
	/// </summary>
	private static bool TryBenchAt(Piece bench, Vector3 anchor, Vector3 piece, Quaternion rot, Component who, float cover,
		out Vector3 spot, out Quaternion benchRot)
	{
		Dictionary<string, object> verdict;
		if (!FindBenchSpot(bench, anchor, rot, who, out spot, out benchRot, out verdict, true))
		{
			return false;
		}
		if (ClippingVerdict(verdict))
		{
			return false;   // a pose the game calls clipping (a log, a tree, a piece of the site): try the next anchor
		}
		if (cover <= 0f)
		{
			return true;
		}
		float dx = spot.x - piece.x;
		float dz = spot.z - piece.z;
		return dx * dx + dz * dz <= cover * cover;
	}

	/// <summary>P1 (04/11/2026): the pose the game calls <c>clipping</c> - the flag of <c>BuildSite.Check</c> or the status
	/// it derives from it. A bench that clips is not a bench with room: another anchor has to be found.</summary>
	private static bool ClippingVerdict(Dictionary<string, object> verdict)
	{
		if (verdict == null)
		{
			return false;
		}
		object flag;
		if (verdict.TryGetValue("clipping", out flag) && flag is bool clips && clips)
		{
			return true;
		}
		object status;
		return verdict.TryGetValue("status", out status) && Convert.ToString(status) == "clipping";
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
		// checks in the loop below (and says nothing when the plan's own bench does not reach this corner). Lote 1: a
		// blueprint with the stations waived places the stair the same way it places the house.
		bool stationNeeded = StationRequired(state);
		if (stationNeeded && stair.m_craftingStation != null
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
				Dictionary<string, object> check = BuildSite.Check(stair, candidate, rot, cs, stationNeeded);
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
				// P2 (multi-build): ALL three paths below skip WearNTear.Destroy, so the route map has to be told here -
				// a stair that came down and stays on the map is a ramp the mover walks up into the air. Read the
				// position before any of them (Destroy is deferred today, but do not rely on it).
				MHR_CompanionNavMesh.InvalidateAround(go.transform.position);
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
		if (PlanLocked(state) || state.Plan == null || state.Placed == null || state.Index + 1 >= state.Plan.Count)
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
		state.PullUntil = state.Index + pulled.Count;
		Log(ai, pulled.Count + " floor piece(s) over the " + state.Prefab + " brought forward so it leads somewhere");
	}

	/// <summary>
	/// E7 (player's report, 03/11/2026: "a ladder with no floor again"): a piece refused with <c>no_support</c> needs what is
	/// directly under it. The pieces in the plan within 1,5 m in XZ and 0,5-2,6 m below it (up to 3) are moved to right after
	/// the current step, so they are built BEFORE it comes back from the retry queue. Nothing is added or removed.
	/// </summary>
	private static bool PullSupports(CompanionAI ai, State state)
	{
		if (PlanLocked(state) || state.Plan == null || state.Index < 0 || state.Index >= state.Plan.Count)
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
			// a roof (3) or a door (4) is never what holds a piece up - and pulling a door would break "doors last"
			// (review, 03/11/2026: one line, a door could become the first piece of the build)
			int cls = BuildBlueprintParser.ClassOf(step.Prefab);
			if (cls == 3 || cls == 4)
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
		PlannedStep current = state.Plan[state.Index];
		for (int i = found.Count - 1; i >= 0; i--)
		{
			state.Plan.RemoveAt(found[i]);
		}
		int at = state.Index;
		state.Plan.RemoveAt(at);
		state.Plan.InsertRange(at, pulled);
		state.Plan.Insert(at + pulled.Count, current);
		// the NEXT Advance takes the first support; the refused piece comes right after them (review, 03/11/2026)
		state.PullUntil = at + pulled.Count - 1;
		state.Index = at - 1;
		Log(ai, pulled.Count + " support piece(s) under the " + state.Prefab + " brought forward so it can stand");
		return true;
	}

	/// <summary>
	/// P1 (04/11/2026, measured with 3 companions on the 985-piece house: ~10 pieces of the upper storey - <c>wood_floor</c>
	/// and <c>wood_roof_45</c> at y 43,8/45,8 - were refused with <c>no_support</c> until the stall watchdog dropped them
	/// after 60 s): in a <b>group</b> the plan is walked by index and nobody may reorder it (<see cref="PlanLocked"/> - the
	/// indices are the site's, shared by the claims), so <see cref="PullSupports"/> does nothing and the piece that would
	/// hold the refused one up may still be <b>ahead</b> in the plan, or it was skipped by whichever worker's turn it was
	/// and nobody went back to it. This drives that support <b>next</b> for this worker: the index is claimed and entered
	/// out of order, the plan itself is not touched (no index moves), and the refused piece keeps its own index and waits in
	/// the site's retry queue as a <b>dependency</b> (no attempt spent) - <see cref="Advance"/> pulls it again from there.
	///
	/// <para>The candidate is the best one of <see cref="BuildSupportBelow"/>: 0,4 to 2,5 m below and up to 2,5 m in XZ, a
	/// <b>structural</b> piece only (<see cref="BuildPieceKind"/>: a chest or a bed under a floor is not support), a door
	/// never (<c>ClassOf</c> 4 - a door stays last) and a <b>roof only for a roof</b> (<c>ClassOf</c> 3: in a gable the row
	/// of a slope is held by the row below it, which is also a roof - the run of 04/11/2026 lost the east gable because the
	/// pull refused every roof and never fired), and never a piece the site already built. Because the drop is always
	/// positive the pull only walks <b>down</b> the plan, so a chain of refusals cannot loop. A support that another companion
	/// holds is left to it (the piece is only retried): the support is already on its way.</para>
	/// </summary>
	private static bool PullDirectSupport(CompanionAI ai, State state)
	{
		if (ai == null || state == null || state.Group == null || state.Plan == null
			|| state.Index < 0 || state.Index >= state.Plan.Count)
		{
			return false;
		}
		int support = DirectSupportIndex(state);
		if (support < 0)
		{
			return false;   // nothing under it that could hold it up: the piece is only retried
		}
		// 11/11/2026 (the Run 9): a support the site already GAVE UP is never driven again - its index is in the skipped set,
		// the workers do not want it, and pulling it would take the whole site back to a piece nobody will build. The
		// emergency post is the way up for the refused piece (TryFallbackPost).
		if (SupportGivenUp(state, support))
		{
			return false;
		}
		// P1 (04/11/2026, incident on the 985-piece house): ONCE per piece. The refused piece goes to the queue, the
		// support is entered; when the refused piece comes back and is refused again (its support was not the one the
		// game wants) the pull would fire again, forever - the run of the 290afc1 build wrote 1.4M lines and froze the
		// game. The cap bounds an order that keeps finding new refusals.
		if (!state.PulledSupport.Add(state.Index) || state.PulledSupport.Count > 24)
		{
			return false;
		}
		if (!MHR_BuildGroup.TryClaim(state.Group, support, ai.GetInstanceID()))
		{
			return false;   // another companion is already building it
		}
		PlannedStep step = state.Plan[support];
		QueueRetry(ai, state, "no_support");
		Log(ai, "apoio imediato: " + step.Prefab + " em (" + F(step.Pos.x) + ", " + F(step.Pos.y) + ", " + F(step.Pos.z)
			+ ") (" + (support + 1) + "/" + state.Plan.Count + ") - a " + state.Prefab + " espera na fila do canteiro");
		EnterStep(state, support);
		return true;
	}

	/// <summary>P1 (04/11/2026): the plan index that is the direct support of the piece this worker is on, or -1 (see
	/// <see cref="PullDirectSupport"/> for the window and for why only <b>below</b> counts).</summary>
	private static int DirectSupportIndex(State state)
	{
		// a piece of the ROOF (class 3) is held by another roof - the row of the slope below it (measured in the run of
		// 04/11/2026: the east gable of the house stayed open because every roof was refused as a support)
		int own = BuildBlueprintParser.ClassOf(state.Prefab);
		int best = -1;
		float bestRank = float.MaxValue;
		for (int i = 0; i < state.Plan.Count; i++)
		{
			if (i == state.Index)
			{
				continue;
			}
			PlannedStep step = state.Plan[i];
			if (step == null || step.Prefab == null)
			{
				continue;
			}
			// only a STRUCTURAL piece holds another one up (walls, beams, poles, floors, stairs) - a chest or a bed under a
			// floor is not what the game reads as support, and pulling it first would only delay the real one
			if (!BuildPieceKind.IsStructural(step.Prefab))
			{
				continue;
			}
			if (!BuildSupportBelow.CanSupport(own, BuildBlueprintParser.ClassOf(step.Prefab)))
			{
				continue;   // a door never holds anything up; a roof holds only a roof (the layer below it)
			}
			if (MHR_BuildGroup.IsDone(state.Group, i))
			{
				continue;   // already standing: the refusal of the piece is not about this one
			}
			float dx = step.Pos.x - state.Pos.x;
			float dz = step.Pos.z - state.Pos.z;
			float drop = state.Pos.y - step.Pos.y;
			if (!BuildSupportBelow.IsDirectSupport(dx, dz, drop))
			{
				continue;
			}
			float rank = BuildSupportBelow.Rank(dx, dz, drop);
			if (rank < bestRank)
			{
				bestRank = rank;
				best = i;
			}
		}
		return best;
	}

	/// <summary>
	/// 11/11/2026 (the emergency post when the plan's own support does not come): the plan piece at
	/// <paramref name="support"/> was <b>given up</b> for the site - the retry queue ran out of real attempts or the stall
	/// watchdog dropped it - so it is never going to hold the refused piece up and the post may take over. In a group the
	/// skipped set is the site's (<see cref="MHR_BuildGroup.GivenUp"/>); a single worker keeps its own.
	/// </summary>
	private static bool SupportGivenUp(State state, int support)
	{
		if (state == null || state.Plan == null || support < 0 || support >= state.Plan.Count)
		{
			return false;
		}
		if (state.Group != null)
		{
			return MHR_BuildGroup.GivenUp(state.Group, support);
		}
		// the piece's identity in the counters is the FIRST place the step appears (the retry pass appends the same step)
		PlannedStep step = state.Plan[support];
		int index = step != null ? state.Plan.IndexOf(step) : support;
		return state.SkippedIndices.Contains(index >= 0 ? index : support);
	}

	/// <summary>11/11/2026 (the emergency post): another companion <b>holds</b> the plan's support right now - it is on its
	/// way and the refused piece only waits for it. A single worker has no claims.</summary>
	private static bool SupportClaimed(State state, int support)
	{
		if (state == null || state.Group == null || support < 0)
		{
			return false;
		}
		return MHR_BuildGroup.OwnerOf(state.Group, support) != 0;
	}

	/// <summary>
	/// 11/11/2026 (the emergency post): how many times the piece this worker is on was deferred for lack of a
	/// <b>dependency</b> - the clock the plan had to deliver the support under it. In a group it is the site's queue
	/// (<see cref="MHR_BuildGroup.DependencyOf"/>); a single worker reads the step itself, which is the very object of its
	/// queue, and falls back to the queue when the pose was adjusted (the step being carried out is then a copy).
	/// </summary>
	private static int SupportDeferrals(State state)
	{
		if (state == null)
		{
			return 0;
		}
		if (state.Group != null)
		{
			return MHR_BuildGroup.DependencyOf(state.Group, state.Index);
		}
		if (state.Plan != null && state.Index >= 0 && state.Index < state.Plan.Count)
		{
			PlannedStep current = state.Plan[state.Index];
			if (SamePiece(current, state))
			{
				return current.Dependency;
			}
		}
		if (state.Retry != null)
		{
			for (int i = 0; i < state.Retry.Count; i++)
			{
				if (SamePiece(state.Retry[i], state))
				{
					return state.Retry[i].Dependency;
				}
			}
		}
		return 0;
	}

	/// <summary>
	/// P1 (04/11/2026, measured with the deck run: 16 pieces of the east gable and of the north-west corner - floors and roofs
	/// from 40,8 to 46,8, all <c>no_support</c> - and the direct-support pull answered -1 for every one of them): the author's
	/// blueprint has <b>no support piece under those points</b> - his house leaned on something the file does not carry - so
	/// the piece can never stand on its own. This plants a wooden post under it, standing on the <b>surface below</b> (the
	/// ground, the floor of the storey under it, a beam top) with its top touching the base of the refused piece.
	///
	/// <para>The post is the <b>shortest one that reaches the gap</b> (<see cref="BuildSupportBelow.PickPost"/>): the first
	/// version used a fixed 2 m beam with its centre 1 m under the piece, so a piece ~1 m above the ground got the beam
	/// <b>buried 1 m</b> and the game refused the beam itself (<c>no_support</c> - measured in the run of 5 companions). The
	/// lengths are read from each prefab's own snap points (<see cref="PostAxis"/>), so the pick cannot drift from the asset.</para>
	///
	/// <para>Validated by the game's own rules (<see cref="BuildSite.Check"/>: the ground, the station in range, no clipping)
	/// and placed the same way any piece is (paid from the bag/chests, free with <see cref="State.Free"/>). The refused piece
	/// keeps its index, its claim and its pose: the next frame it is placed on top of the post - so a column closes <b>from
	/// the bottom up</b> (the floor at 40,8 gets a post on the ground, the next one a post on that floor, and so on).</para>
	///
	/// <para>One per piece (<see cref="State.FallbackPosts"/>) and capped per order (<see cref="MaxFallbackPosts"/>). A support
	/// the plan <b>has</b> under the piece (<see cref="DirectSupportIndex"/> &gt;= 0) usually blocks the post - the pull is the
	/// path - but only while that support is <b>really coming</b>: the Run 9 left three floors frozen because the support was
	/// in the plan, was refused by a body and never went up, and the guard kept refusing the post. So the post is allowed when
	/// the support was <b>given up</b> or when nobody holds it and the piece already waited
	/// <see cref="BuildSupportBelow.SupportGrace"/> deferrals for it (<see cref="BuildSupportBelow.FallbackPostAllowed"/>).
	/// Anything that stops it (no post reaches the gap, no valid pose, no material, the executor) is said out loud and the
	/// piece follows the normal path (retry queue / watchdog).</para>
	/// </summary>
	private static bool TryFallbackPost(CompanionAI ai, State state, CompanionSetup cs)
	{
		if (ModConfig.BuildSupportFallback == null || !ModConfig.BuildSupportFallback.Value)
		{
			return false;
		}
		if (ai == null || state == null || state.Blueprint == null || state.Plan == null || state.Piece == null)
		{
			return false;
		}
		if (state.FallbackPosts.Count >= MaxFallbackPosts)
		{
			return false;   // the order already planted its posts
		}
		// P1 (11/11/2026): furniture never gets a post. A chest/bed/fireplace is not what a post holds up (the plan's floor
		// does), and those prefabs have m_noClipping, so the game refuses the post next to them as `clipping` - the post
		// would be paid and thrown away. Only the structural pieces of the plan are worth a post under them.
		if (!BuildPieceKind.IsStructural(state.Prefab))
		{
			return false;
		}
		int index = PieceIndex(state);
		if (index < 0 || state.FallbackPosts.Contains(index))
		{
			return false;   // this piece already got one
		}
		// 11/11/2026 (the Run 9: the plan's support was there but NOT in the world): a support of the plan only blocks the
		// post while it is really coming. Given up (skipped) it never will; claimed by a companion it is on its way; and a
		// piece that already waited SupportGrace deferrals for it (8 s + 12 s) has given the plan its chance.
		int support = DirectSupportIndex(state);
		if (support >= 0
			&& !BuildSupportBelow.FallbackPostAllowed(SupportGivenUp(state, support), SupportClaimed(state, support),
				SupportDeferrals(state)))
		{
			return false;   // the plan has something under it and it is coming: the pull is the path (never a post over it)
		}
		// where does the post stand? The surface under the piece's own point: the ground, the floor of the storey below, a
		// beam top. Nothing in 10 m answers the height map (fail-open: the spot is on a hill side).
		//
		// P1 (04/11/2026, measured in the run of 5: "vão de 0,16 m" 318 times): a single ray stopped on a ROOF EAVE 0,16 m
		// under a floor of 40,8, and no post fits in 16 cm - while the useful surface (the deck floor at 39,78) was 1,02 m
		// down. So the ray is walked down THROUGH the surfaces that are too close to be a base, each pass starting 5 cm
		// under the last hit; the reach (PostReach) is the total of the whole walk, not of each pass.
		//
		// P1 (11/11/2026, the run that closed at 950 of 987): depth was not the only thing to walk through - the surface also
		// has to HOLD. The top of a chest (furniture has m_supports = false in 1.0.20) was taken as a base, the post was
		// accepted over it, the game refused the post itself with `no_support` (95 times) and 20 pieces never went up. Each
		// hit now carries its verdict (s_postHolds) and the walk goes on through a shallow surface OR one that cannot hold.
		int terrain = LayerMask.NameToLayer("terrain");
		Vector3 piece = state.Pos;
		float bottom = piece.y - PostReach;
		float from = piece.y + 0.05f;
		int hits = 0;
		int skipped = 0;
		for (int pass = 0; pass < MaxPostPasses && from > bottom; pass++)
		{
			RaycastHit hit;
			if (!Physics.Raycast(new Vector3(piece.x, from, piece.z), Vector3.down, out hit, from - bottom,
				BuildProbe.PlaceMask(), QueryTriggerInteraction.Ignore))
			{
				break;
			}
			float depth = piece.y - hit.point.y;
			bool holds = PostBaseHolds(hit.collider, terrain);
			s_postDepths[hits] = depth;
			s_postHolds[hits] = holds;
			hits++;
			if (depth >= MinPostGap && holds)
			{
				break;   // deep enough AND something that can hold the post: this is its base
			}
			skipped++;
			from = hit.point.y - 0.05f;   // 5 cm under the surface that was too close (a roof eave, a lying beam, a chest)
		}
		int useful = BuildSupportBelow.UsefulSurface(s_postDepths, s_postHolds, hits, MinPostGap);
		float baseY = useful >= 0
			? piece.y - s_postDepths[useful]
			: (ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(piece) : piece.y - 2f);
		float gap = piece.y - baseY;
		MeasurePosts();
		int pick = BuildSupportBelow.PickPost(s_postLengths, gap);
		if (pick < 0)
		{
			Log(ai, "sem lugar para o apoio de emergência sob " + state.Prefab + " em (" + F(piece.x) + ", " + F(piece.y)
				+ ", " + F(piece.z) + ") - vão de " + gap.ToString("F2") + " m"
				+ (skipped > 0 ? " (" + skipped + " superfície(s) rasa(s) ou sem apoio ignorada(s))" : string.Empty));
			return false;
		}
		float length = s_postLengths[pick];
		Piece post = BuildProbe.PieceOf(FallbackPostPrefabs[pick]);
		if (post == null)
		{
			return false;
		}
		Vector3 pos = new Vector3(piece.x, baseY + length * 0.5f, piece.z);
		Quaternion rot = VerticalPose(post);
		Dictionary<string, object> check = BuildSite.Check(post, pos, rot, Self(ai), StationRequired(state));
		if (!Ok(check) && !(useful >= 0 && OnlyNoSupport(check)))
		{
			Log(ai, "sem lugar para o apoio de emergência sob " + state.Prefab + " em (" + F(pos.x) + ", " + F(pos.y) + ", "
				+ F(pos.z) + ") - " + (check != null ? Convert.ToString(check["status"]) : "?") + " (vão "
				+ gap.ToString("F2") + " m)");
			return false;
		}
		Inventory bag = BagOf(ai);
		bool useChests = ModConfig.BuildUseChests == null || ModConfig.BuildUseChests.Value;
		Vector3 stock = state.AnchorKnown ? state.Anchor : state.Pos;
		Dictionary<string, int> missing;
		if (!state.Free && !BuildStock.Take(bag, post, stock, cs, useChests, out missing))
		{
			Log(ai, "sem material para o apoio de emergência sob " + state.Prefab + " (" + MissingText(missing) + ")");
			return false;
		}
		BuildResult result = BuildExecutor.Place(cs, post, pos, rot);
		if (!result.Ok)
		{
			if (!state.Free)
			{
				Refund(bag, post);   // the post never went up: the wood goes back (with Free nothing was taken)
			}
			Log(ai, "sem lugar para o apoio de emergência sob " + state.Prefab + " em (" + F(pos.x) + ", " + F(pos.y) + ", "
				+ F(pos.z) + ") - " + result.Error + " (vão " + gap.ToString("F2") + " m)");
			return false;
		}
		state.FallbackPosts.Add(index);
		Log(ai, "apoio de emergência: " + FallbackPostPrefabs[pick] + " (" + length.ToString("F2") + " m) sob "
			+ state.Prefab + " em (" + F(pos.x) + ", " + F(pos.y) + ", " + F(pos.z) + ")");
		return true;
	}

	/// <summary>
	/// P1 (11/11/2026): can the surface this ray hit be the <b>base a post stands on</b>? The class-2 verdict of
	/// <see cref="BuildSupportBelow.HoldsPlacedPiece"/> - the four emergency posts are beams/poles (class 2), and the rule
	/// asks what holds <b>that</b> piece: the terrain holds, a piece only when it carries support (<c>m_supports</c>), and a
	/// collider with no <see cref="WearNTear"/> does not. That is what refuses the tops of <b>chests/beds/fireplaces</b>
	/// (<c>m_supports = false</c>, measured in 1.0.20) - the base the post had in the run of 11/11, which made the post
	/// itself a piece with nothing under it and the game refused it with <c>no_support</c>. The rule itself is left untouched
	/// on purpose: the reverse engineering of 1.0.20 governs it (<c>docs/re/support-rule-wearntear.md</c>), and a roof
	/// carries support like any other structural piece.
	/// </summary>
	private static bool PostBaseHolds(Collider collider, int terrainLayer)
	{
		if (collider == null)
		{
			return false;
		}
		bool isTerrain = terrainLayer >= 0 && collider.gameObject.layer == terrainLayer;
		WearNTear wear = isTerrain ? null : collider.GetComponentInParent<WearNTear>();
		return BuildSupportBelow.HoldsPlacedPiece(2, isTerrain, wear != null, wear != null && wear.m_supports);
	}

	/// <summary>P1 (04/11/2026): fills <see cref="s_postLengths"/> from the prefabs once - the pieces do not change, and a
	/// <c>no_support</c> event must not scan the prefabs on every refusal.</summary>
	private static void MeasurePosts()
	{
		if (s_postsMeasured)
		{
			return;
		}
		bool any = false;
		for (int i = 0; i < FallbackPostPrefabs.Length; i++)
		{
			Piece post = BuildProbe.PieceOf(FallbackPostPrefabs[i]);
			if (post == null)
			{
				continue;   // this game does not have it: the pick skips the 0
			}
			s_postLengths[i] = PostAxis(post).magnitude;
			any = true;
		}
		// only latch when something was really measured: a call before the scene is ready must not freeze four zeroes
		s_postsMeasured = any;
	}

	/// <summary>P1 (04/11/2026): the pose that stands a post up - and the axis it stands along. The piece's own <b>snap
	/// points</b> are its tips, so the widest pair is the long axis in the prefab's own space; rotating that axis onto the
	/// world up is the vertical pose, and its length is what the post really is (the run of 04/11/2026 planted a 2 m beam
	/// under a 1 m gap). No guessing which local axis the artist used (the straight beam and the 45 degree one are not
	/// authored on the same axis, measured in <c>docs/re/building-notes.md</c> §11).</summary>
	private static Vector3 PostAxis(Piece post)
	{
		s_snaps.Clear();
		post.GetSnapPoints(s_snaps);
		Vector3 axis = Vector3.zero;
		float longest = 0f;
		for (int i = 0; i < s_snaps.Count; i++)
		{
			for (int j = i + 1; j < s_snaps.Count; j++)
			{
				Transform a = s_snaps[i];
				Transform b = s_snaps[j];
				if (a == null || b == null)
				{
					continue;
				}
				Vector3 delta = post.transform.InverseTransformPoint(b.position) - post.transform.InverseTransformPoint(a.position);
				if (delta.sqrMagnitude > longest)
				{
					longest = delta.sqrMagnitude;
					axis = delta;
				}
			}
		}
		s_snaps.Clear();
		return axis;
	}

	/// <summary>P1 (04/11/2026): the rotation that puts <paramref name="post"/>'s own long axis along the world up. A prefab
	/// with no pair of snap points gets a quarter turn about Z, the axis the beam family is authored on.</summary>
	private static Quaternion VerticalPose(Piece post)
	{
		Vector3 axis = PostAxis(post);
		return axis.sqrMagnitude > 0.01f ? Quaternion.FromToRotation(axis.normalized, Vector3.up) : Quaternion.Euler(0f, 0f, 90f);
	}

	/// <summary>P1 (04/11/2026): scratch of <see cref="PostAxis"/> - the snap points are filled by the game into a list,
	/// and a no_support event must not allocate one (main thread only).</summary>
	private static readonly List<Transform> s_snaps = new List<Transform>();

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
		// P1 (multi-build): a new order replaces the running one, and a member of a group must not keep its seat (nor a
		// claim) in a site it is no longer building
		if (old.Group != null)
		{
			MHR_BuildGroup.ReleaseAllOf(old.Group, ai.GetInstanceID());
			MHR_BuildGroup.Leave(old.Group, ai.GetInstanceID());
			old.Group = null;
		}
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
		// Lote 1 (11/11/2026): a blueprint with [Building] BlueprintStations on is built without stations - the refusals
		// below and the pose search follow that, otherwise every candidate would fail with needs_station and the piece
		// would never be placed.
		bool stationNeeded = StationRequired(state);
		// a piece that needs a station (a chest, a bed) is refused with its own line BEFORE the search: without the
		// station every candidate would fail with needs_station, and "no place for it here" would be a lie - the
		// player needs a workbench, not more room.
		if (stationNeeded && state.Piece != null && state.Piece.m_craftingStation != null
			&& CraftingStation.HaveBuildStationInRange(state.Piece.m_craftingStation.m_name, state.Hint) == null)
		{
			string station = Localization.instance != null ? Localization.instance.Localize(state.Piece.m_craftingStation.m_name) : state.Piece.m_craftingStation.m_name;
			Log(ai, "needs a station: " + station + " is not in range of the spot");
			Finish(ai, state, "hc_ai_build_needs_station", "station out of range", station);
			return false;
		}
		float radius = ModConfig.BuildSiteSearchRadius != null ? ModConfig.BuildSiteSearchRadius.Value : 8f;
		BuildSiteResult site = BuildSite.Find(state.Piece, state.Hint, state.Forward, radius, 3, 8, 48, Self(ai), stationNeeded);
		state.Tried = site.Tried;
		if (!site.Ok)
		{
			Log(ai, "no site: " + site.Tried + " tried, last " + site.Status + " " + TallyText(site.Tally));
			// the search may still end on needs_station when the station is near the player but not near the spot
			if (stationNeeded && state.Piece != null && state.Piece.m_craftingStation != null && site.Tally.ContainsKey("needs_station"))
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
		state.WalkFor = -1;   // 11/11/2026 (Run 13): a new pose is a new walk spot
		state.HasWalkTo = false;
		state.Phase = Phase.Goto;
		Log(ai, "site at (" + F(site.Pos.x) + ", " + F(site.Pos.y) + ", " + F(site.Pos.z) + "), " + F(site.Distance) + " m from the hint, "
			+ site.Tried + " tried" + (site.Tally.Count > 0 ? " (" + TallyText(site.Tally) + ")" : string.Empty));
		return true;
	}

	private static bool Goto(CompanionAI ai, State state, CompanionSetup cs, float dt)
	{
		// E7 (review, 03/11/2026): a resumed order starts at the piece where it stopped, which after a fall/death can be
		// 10-17 m from the companion; reorder the CURRENT piece once, before the walk, or the retry pass burns 12 s first.
		if (!state.FloorPreferDone && state.Blueprint != null && state.Index >= 0)
		{
			state.FloorPreferDone = true;
			if (PreferNearFloor(ai, state, state.Index))
			{
				PlannedStep current = state.Plan[state.Index];
				state.Prefab = current.Prefab;
				state.Piece = current.Piece;
				state.Pos = current.Pos;
				state.Rot = current.Rot;
				state.BestDistance = 0f;
				state.BestAt = Time.time;
				state.WalkFor = -1;   // the piece at this index changed: the spot is chosen again for the new pose
				state.HasWalkTo = false;
			}
		}
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
			Dictionary<string, object> early = BuildSite.Check(state.Piece, state.Pos, state.Rot, cs, StationRequired(state));
			if (early != null && !Ok(early) && Convert.ToString(early["status"]) == "no_support")
			{
				// E7 (player's report, 03/11/2026: "a ladder with no floor again"): the floor over a ladder is pulled forward and
				// then refused with no_support because the frame UNDER it is still ahead in the plan. The supports come first and
				// the refused piece is tried again RIGHT AFTER them - not at the end of the plan (review, 03/11/2026: the ladder
				// stayed without its floor until the whole house was done).
				if (PullSupports(ai, state))
				{
					return Advance(ai, state);
				}
				// P1 (04/11/2026): in a GROUP the plan is not reordered, so PullSupports above did nothing - the support under
				// the piece is driven NEXT by this worker, by index, and the piece waits in the site's queue (no attempt spent)
				if (PullDirectSupport(ai, state))
				{
					return true;   // the support is the step being carried out now
				}
				// P1 (04/11/2026): the plan has NOTHING under it (the author's file leans on something it does not carry) -
				// plant a wooden post once per piece and try the piece again at the same pose
				if (TryFallbackPost(ai, state, cs))
				{
					return true;   // the post is standing: the piece is placed on top of it
				}
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
				// P1 (04/11/2026): before giving the piece up, ONE adjusted pose inside the structural 1 m (the walk to the
				// plan's pose goes over a hole / past the edge - a gable top above the void). The set is the piece's plan
				// index, so the second pair of falls skips it as before.
				if (AdjustFallenPose(ai, state))
				{
					state.FallDrops = 0;
					state.LastY = me.y;   // the drop he just took is counted: the next frame starts a new pair
					return true;          // walking to the adjusted pose
				}
				Log(ai, "falling from the way to the " + state.Prefab + " - skipping it (" + state.FallDrops + " fall(s))");
				SkipPiece(ai, state, "falls");
				state.FallDrops = 0;
				return Advance(ai, state);
			}
		}
		state.LastY = me.y;
		// 11/11/2026 (Run 13, the ridge of the CommHomeBasicBP): the walk used to be sent at the piece's own pose - a roof
		// row 10 m above the feet - and Pathfinding.GetPath snaps the "from" point to the navmesh up to 12 m, so the route
		// started far from the feet and walked the WRONG way (measured: no progress after 12 s at 22,8 m and then 23,5 m).
		// Now the walk goes to a SURFACE the piece can be reached from (the plan's own floor/roof row below it), chosen once
		// per piece. state.Pos stays the PIECE for the placement checks, the falls and the claims - only the target of the
		// walk changes.
		if (ReachWalk && state.Pos.y > me.y + 1f)
		{
			if (state.WalkFor != state.Index)
			{
				state.WalkFor = state.Index;
				Vector3 walk;
				state.HasWalkTo = WalkSpot(ai, state, out walk);
				if (state.HasWalkTo)
				{
					state.WalkTo = walk;
					LogDebug(ai, "walk spot (" + F(walk.x) + ", " + F(walk.y) + ", " + F(walk.z) + ") - the " + state.Prefab
						+ " is " + F(state.Pos.y - walk.y) + " m above that surface (" + state.WalkFound + " superfície(s) em "
						+ state.WalkRays + " raio(s))");
				}
				else
				{
					LogDebug(ai, "sem walk spot para a peça - " + state.WalkFound + " superfície(s) em " + state.WalkRays
						+ " raio(s)" + (state.WalkFound == 0 ? " (nenhuma superfície estrutural no alcance dos raios)" : string.Empty));
				}
			}
		}
		else if (state.HasWalkTo || state.WalkFor >= 0)
		{
			// the switch was turned off or the piece is at the feet: the piece itself is the target (the eye rule covers it)
			state.HasWalkTo = false;
			state.WalkFor = -1;
		}
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
			// only helps when the piece is ABOVE the eye rule (<see cref="MaxPlace"/> - vanilla 5 m, [Building] PlaceReach) -
			// if he is stuck for any other reason (a wall, a hole) it would be junk. And it is placed only here, after 12 s
			// of real struggle, never just because he walked past.
			Vector3 eyeNow = new Vector3(me.x, me.y + EyeHeight, me.z);
			if (new Vector2(me.x - state.Pos.x, me.z - state.Pos.z).magnitude <= MaxPlace
				&& Vector3.Distance(eyeNow, state.Pos) > MaxPlace
				&& TryScaffold(ai, state, cs))
			{
				return true;
			}
			// 11/11/2026 (Run 13): the clock does not run while the mover is climbing, waiting for a ladder, on a ladder
			// chain or on a detour - going UP does not get closer to a piece 20 m away, and cutting the climb here would
			// throw the piece to the retry queue in the middle of the only way it has.
			if (MHR_Mover.Vertical(ai))
			{
				state.BestAt = Time.time;
			}
			else
			{
				Log(ai, "no progress towards the " + state.Prefab + " for 12 s (" + F(walkGap) + " m away"
					+ (state.HasWalkTo
						? ", walk spot (" + F(state.WalkTo.x) + ", " + F(state.WalkTo.y) + ", " + F(state.WalkTo.z) + ")"
						: ", walk spot -")
					+ (state.WalkRays > 0
						? ", " + state.WalkFound + " superfície(s) em " + state.WalkRays + " raio(s)"
						: ", sem busca de superfície")
					+ ") - retrying it later, I am at (" + F(me.x) + ", " + F(me.y) + ", " + F(me.z) + "), route "
					+ (MHR_Mover.RoutePartial(ai) ? "partial" : "full"));
				QueueRetry(ai, state, "no progress");
				return Advance(ai, state);
			}
		}
		float flat = new Vector2(me.x - state.Pos.x, me.z - state.Pos.z).magnitude;
		// a piece may be placed up to <see cref="MaxPlace"/> from the EYE (vanilla Player.m_maxPlaceDistance = 5 m, and
		// [Building] PlaceReach raises it); the companion's eye is ~1,5 m above its feet, so a roof 4,9 m up is within
		// reach even though the feet are 4,9 m below it
		Vector3 eye = new Vector3(me.x, me.y + EyeHeight, me.z);
		// E7 (player's report, 03/11/2026: "he is still struggling to place a piece on the high roof"): the flat cap was
		// Reach + 0,5 = 3,0 m while the game allows 5 m from the EYE - the gable top at 42,8 with the companion standing
		// 3,15 m away flat (3,3 m in 3D) was refused by 15 cm and he walked in circles for 12 s. The eye rule is the
		// game's own (now configurable); the flat cap only has to keep him from placing through the far side of the house.
		bool inReach = flat <= Reach + 0.5f && Vector3.Distance(eye, state.Pos) <= MaxPlace;
		// E7 (player's report, 03/11/2026: "he is still struggling to place a piece on the high roof"): the flat cap of 3,0 m
		// was stricter than the eye rule and refused a gable top 3,15 m away by 15 cm. The relaxed cap (<c>MaxPlace - 0,5</c>,
		// which is 4,5 m at the vanilla 5 and 9,5 m at the default 10) is used ONLY when he stopped getting closer for 3 s:
		// the normal walk still ends at Reach, so a piece is not placed from far away just because he walked past the radius.
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
		// E7 (player's idea, 03/11/2026): the "pulinho" - a player jumps to reach a beam that is just above the eye rule
		// (<see cref="MaxPlace"/> - vanilla 5 m, [Building] PlaceReach); the eye goes up with the body, so the same rule
		// then covers it. Only with the feet already in place and the piece at most 1 m beyond the reach (a hop, not a
		// flight), with a cooldown so it is not a bunny hop.
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
		MHR_Mover.Go(ai, dt, state.HasWalkTo ? state.WalkTo : state.Pos, Reach, true, "Build");
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

	/// <summary>
	/// 11/11/2026 (Run 13, the ridge of the <c>CommHomeBasicBP</c>): the surface the walk goes to for the current piece -
	/// the spot the piece can be placed <b>from</b>, instead of the piece's own pose in mid-air. The surfaces are found with
	/// rays <b>down</b> from just above the piece: its own column (<c>r = 0</c>) and eight directions at each radius of the
	/// <see cref="s_walkRadii"/> rings - <b>17 rays per piece</b>, cast once per plan index (a new pose, <c>EnterStep</c> or
	/// a nudge, asks again), never per frame.
	///
	/// <para>Only a surface that can hold a companion counts: the <b>terrain</b>, or a <b>structural</b> piece (a
	/// <c>Piece</c> with <c>WearNTear</c> that is not furniture - <see cref="BuildPieceKind.IsStructural"/>). A body
	/// (<c>Character</c>/<c>Player</c>) on the spot is not a floor and is discarded, and so is furniture: the top of a chest
	/// is not a place to build a roof from. The choice itself is <see cref="BuildWalkSpot.Choose"/> (pure, asserted by the
	/// harness): the same storey first (the shortest walk), then the highest surface within the eye rule. The eye rule
	/// (<see cref="MaxPlace"/>, <c>[Building] PlaceReach</c>) is the ceiling - the rings widen <b>where</b> the surfaces are
	/// looked for, never how far the piece may be placed from.</para>
	///
	/// <para>11/11/2026 (Run 14, measured: the walk spot of a <c>wood_roof_45</c> at 48,8 was the floor at <b>41,9</b> - 7 m
	/// below, 20 m away - and the companion never got closer): the rings were 0,8/1,6/2,4 m and each column took only its
	/// <b>first</b> hit. Two things were lost: the row the piece is placed from can be <b>beside</b> it (the top rows have the
	/// gable under the ridge, not a floor), and a column whose first hit is not a surface - a <b>body</b> of a companion
	/// walking over the sample, the row above the piece - threw the whole column away. Now the ring reaches 1,5 m and 3 m and
	/// a column walks down past up to <see cref="WalkHits"/> hits that are not a surface. Both counters
	/// (<see cref="State.WalkRays"/>, <see cref="State.WalkFound"/>) are in the log, so "where did it look" is never a
	/// guess.</para>
	/// </summary>
	private static bool WalkSpot(CompanionAI ai, State state, out Vector3 spot)
	{
		spot = state != null ? state.Pos : Vector3.zero;
		if (ai == null || state == null || state.Piece == null)
		{
			return false;
		}
		int mask = BuildProbe.PlaceMask();
		int terrain = LayerMask.NameToLayer("terrain");
		float top = state.Pos.y + 0.6f;
		float bottom = state.Pos.y - WalkReach;
		s_walkSurfaces.Clear();
		s_walkSeen.Clear();
		state.WalkRays = 0;
		// sample 0 is the piece's own column; 1.. are the eight directions at each radius of s_walkRadii
		for (int sample = 0; sample <= 8 * s_walkRadii.Length; sample++)
		{
			float x = state.Pos.x;
			float z = state.Pos.z;
			if (sample > 0)
			{
				int i = sample - 1;
				float radius = s_walkRadii[i / 8];
				float angle = Mathf.PI * 2f * (i % 8) / 8f;
				x += Mathf.Cos(angle) * radius;
				z += Mathf.Sin(angle) * radius;
			}
			float y = top;
			for (int hit = 0; hit < WalkHits; hit++)
			{
				RaycastHit found;
				state.WalkRays++;
				if (!Physics.Raycast(new Vector3(x, y, z), Vector3.down, out found, y - bottom, mask,
					QueryTriggerInteraction.Ignore))
				{
					break;   // nothing else under this column
				}
				if (found.point.y <= state.Pos.y + BuildWalkSpot.SameStoreyAbove
					&& StructuralSurface(found.collider, terrain))
				{
					NoteSurface(found.collider, found.point);
					break;   // the first surface of the column is the one it would stand on
				}
				// not a surface (a body over the sample, a piece above this one, furniture): go on under it
				y = found.point.y - 0.05f;
				if (y <= bottom)
				{
					break;
				}
			}
		}
		state.WalkFound = s_walkSurfaces.Count;
		return BuildWalkSpot.Choose(ai.transform.position, state.Pos, MaxPlace, s_walkSurfaces, out spot);
	}

	/// <summary>
	/// 11/11/2026 (Run 14): note a surface of the search <b>once</b>. Two rays of the rings land on the same floor/roof row
	/// all the time (a plane is 2 m wide); the key is the collider and the height of the point, so the same plane found from
	/// two directions is one surface (the pure choice does not care about duplicates, but the count in the log must mean
	/// something).
	/// </summary>
	private static void NoteSurface(Collider collider, Vector3 point)
	{
		if (collider == null)
		{
			return;
		}
		int key = unchecked(collider.GetInstanceID() * 397) ^ Mathf.RoundToInt(point.y * 100f);
		if (s_walkSeen.Add(key))
		{
			s_walkSurfaces.Add(point);
		}
	}

	/// <summary>
	/// 11/11/2026 (Run 13): is this collider a surface the companion may stand on to place a piece - the <b>terrain</b> or a
	/// <b>structural</b> piece? The same rule the emergency post uses for its base (<see cref="PostBaseHolds"/>), with the
	/// structural filter on top so a <b>chest</b>, a bed or a bench top is never taken as a floor (the game gives them
	/// <c>m_supports = false</c>, they are not something to climb). A character is discarded first: a companion standing
	/// over the sample is not the floor under it.
	/// </summary>
	private static bool StructuralSurface(Collider collider, int terrainLayer)
	{
		if (collider == null)
		{
			return false;
		}
		if (terrainLayer >= 0 && collider.gameObject.layer == terrainLayer)
		{
			return true;
		}
		if (collider.GetComponentInParent<Character>() != null)
		{
			return false;   // a body over the sample point: not a surface
		}
		Piece piece = collider.GetComponentInParent<Piece>();
		if (piece == null || piece.GetComponent<WearNTear>() == null)
		{
			return false;
		}
		return BuildPieceKind.IsStructural(Utils.GetPrefabName(piece.gameObject));
	}

	/// <summary>
	/// 11/11/2026 (the body barrier): the pose was refused because a <b>body</b> (the player walking over the spot) was on
	/// it. Nothing was paid (the refusal is before <c>BuildStock.Take</c>) and the claim stays this worker's
	/// (<see cref="MHR_BuildGroup.Refresh"/> runs on every frame of the wait, so the 20 s expiry never sees it) - after
	/// <see cref="BuildFailure.ShortRetryDelay"/> the piece is simply tried again at the <b>same</b> pose. The site's
	/// barrier (<see cref="MHR_BuildGroup.NoteBody"/>) is what holds the storeys above while this happens.
	/// </summary>
	private static bool BodyRetry(CompanionAI ai, State state)
	{
		if (state == null)
		{
			return false;
		}
		if (Time.time - state.Start < BuildFailure.ShortRetryDelay)
		{
			return true;   // the player is moving - a moment, not a timeout
		}
		state.Phase = Phase.Place;
		return true;
	}

	private static bool Place(CompanionAI ai, State state, CompanionSetup cs, Inventory bag)
	{
		// P1 (multi-build, 03/11/2026): the claim is the right to pay and place the piece. When it is not this worker's
		// (it was not driven for 20 s - a fight, a load to a chest - and the claim expired), the pose is asked of the world
		// BEFORE paying again: a piece another companion already built is never placed twice.
		// P1 (review B1, 03/11/2026): the pose is asked for a piece that ALREADY FAILED once as well, even when the claim
		// is ours: a piece that was placed and then lost its support stays in the world, and the retry would put a second
		// one at the same pose (the ghost check only sees clipping for m_noClipping; a piece without it passes and gets
		// paid twice). The single-worker retry pass has the same hole (state.Passes > 0).
		if (state.Group != null)
		{
			int holder = MHR_BuildGroup.OwnerOf(state.Group, state.Index);
			bool mine = holder == ai.GetInstanceID();
			if (!mine || MHR_BuildGroup.Retried(state.Group, state.Index))
			{
				GameObject existing;
				long creator = BuildExecutor.CreatorOf(cs, Player.m_localPlayer);
				// P1 (review A6): the pose AND the orientation - a neighbouring piece of the same prefab is not this one.
				// checkStation is not asked here: a bench placed a second ago is not in the registry yet and it IS there.
				if (state.Piece != null
					&& BuildExecutor.VerifyAt(state.Piece, state.Pos, state.Rot, creator, out existing) == null)
				{
					CountPlaced(state);   // P1 (review A7): the group's index is marked done - counted once
					Log(ai, "a peça " + (state.Index + 1) + "/" + state.Plan.Count
						+ " já está de pé (outra tentativa a deixou lá) - seguindo");
					return Advance(ai, state);
				}
			}
			if (!mine && (holder != 0 || !MHR_BuildGroup.TryClaim(state.Group, state.Index, ai.GetInstanceID())))
			{
				if (CompanionsPlugin.LogNormal)
				{
					LogDebug(ai, "a peça " + (state.Index + 1) + "/" + state.Plan.Count + " passou para outro companheiro");
				}
				return Advance(ai, state);
			}
		}
		else if (state.Passes > 0 && state.Plan != null)
		{
			// the legacy retry pass re-walks the pieces that did not stand - the same "is it already there" question
			GameObject existing;
			long creator = BuildExecutor.CreatorOf(cs, Player.m_localPlayer);
			if (state.Piece != null
				&& BuildExecutor.VerifyAt(state.Piece, state.Pos, state.Rot, creator, out existing) == null)
			{
				CountPlaced(state);
				Log(ai, "a peça " + (state.Index + 1) + "/" + state.Plan.Count
					+ " já está de pé (retry) - seguindo");
				return Advance(ai, state);
			}
		}
		// E6: a plan step is checked AGAIN now, with the pieces the plan already built standing - the roof's support
		// comes from the walls, and up-front they were only planned (measured: the roof was refused with no_support)
		if ((state.Template != null || state.Blueprint != null) && state.Plan != null && state.Index > 0)
		{
			// Lote 1 (11/11/2026): a blueprint with [Building] BlueprintStations on places its pieces without a workbench, so
			// this check must not report needs_station for it (the flag is the order's, and it is the one that keeps the
			// piece from looping in the retry queue forever).
			Dictionary<string, object> check = BuildSite.Check(state.Piece, state.Pos, state.Rot, cs, StationRequired(state));
			if (!Ok(check))
			{
				string status = check != null ? Convert.ToString(check["status"]) : "unknown";
				// P1 (04/11/2026, measured on the 985-piece house): a piece refused by the SPOT (too_steep/no_ground - the bank
				// the 32 m leveling did not reach) gets ONE chance at the closest valid pose around the plan's one, before the
				// normal refusal; a structural piece only moves 1 m, furniture up to 5 m (3 rings x 12 on the second try).
				// P1 (04/11/2026, second run): a CLIPPING furniture piece gets the same chance - the plan is validated before
				// the clearing and the leveling, so a log that rolled onto the anchor (the bench at -178,3, 38,6, -431,7) is
				// the world changing under the plan, not a bad pose (see NudgePiece).
				if ((status == "too_steep" || status == "no_ground" || status == "clipping") && NudgePiece(ai, state, status))
				{
					return true;   // the next frame re-checks (and places) at the adjusted pose
				}
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
				if (status == "needs_station" && StationRequired(state) && state.Blueprint != null && state.Plan != null && state.Piece != null
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
					// P1 (04/11/2026): a refusal by no_support in a GROUP gets its direct support driven next (the same pull
					// the early check does) - the plan is not reordered and the piece waits in the site's queue
					if (status == "no_support" && PullDirectSupport(ai, state))
					{
						return true;   // the support is the step being carried out now
					}
					// P1 (04/11/2026): and when the plan has nothing under it, a wooden post is planted once (see TryFallbackPost)
					if (status == "no_support" && TryFallbackPost(ai, state, cs))
					{
						return true;   // the post is standing: the piece is tried again at the same pose
					}
					// 11/11/2026 (the body barrier - the player's rule: "they may skip, but come back and try again before
					// going up"): a spot blocked by a BODY is not the piece failing. Nothing was paid yet (the refusal is
					// before the stock is taken), the claim stays, the site's barrier holds the storeys above, and the SAME
					// pose is tried again in a moment (3 short retries per piece). Without this the entrance steps (pieces
					// 8-10 of 988 in the Run 9) went to the site's queue and only came back after the whole house.
					if (status == "blocked_by_player")
					{
						BlockedByBody(ai, state, cs);
						if (AskStepAside(ai, state, cs))
						{
							// 11/11/2026 (Run 14): there is nowhere free to step to, or the same pair is taking turns from
							// piece to piece - the piece goes to the site's queue now (waiting through the 3 shorts for a
							// companion nobody asked would only repeat the ping-pong). The body barrier is still drawn: a body
							// holds this storey exactly as in TryBodyRetry, so the plan does not climb over the piece.
							MHR_BuildGroup.NoteBody(state.Group, state.Index);
							QueueRetry(ai, state, status);
							return Advance(ai, state);
						}
					}
					if (TryBodyRetry(ai, state, status))
					{
						return true;   // the same pose, after the short wait
					}
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
		// P1 MVP (multi-build, 03/11/2026): there is NO per-piece material reservation between the workers of a group -
		// each one pays from its own bag and the site's chests with its own shortfall, so the same stack may be counted
		// twice and the worker that arrives second is the one that hands the order over to the gather (it comes back and
		// claims another piece). The `Stock` of docs/plan-multi-build.md §3 is the open half of P1.
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
		Log(ai, "placed (" + result.Route + ") em (" + F(state.Pos.x) + ", " + F(state.Pos.y) + ", " + F(state.Pos.z)
			+ "), verifying next frame");
		return true;
	}

	/// <summary>
	/// 11/11/2026 (the body barrier, the player's rule: "they may skip, but come back and try again before going up"): the
	/// spot was refused by a <b>body</b> (the player walking over it). The piece is not lost and the plan does not go up on
	/// it: the site notes the storey (the first refusal draws the barrier) and the worker keeps the piece - same index, same
	/// pose, <b>nothing paid</b> (the refusal is before <c>BuildStock.Take</c>) - and waits
	/// <see cref="BuildFailure.ShortRetryDelay"/> for one more try. The claim survives the wait because
	/// <c>MHR_BuildGroup.Refresh</c> runs every frame of it (the 20 s expiry is for a worker that went away).
	///
	/// <para>False when the refusal is not a body (<see cref="BuildFailure.IsTransient"/>: only <c>blocked_by_player</c>),
	/// when the config is off, or when the short retries of this piece are used up (3 per piece, counted on the site for a
	/// group) - then the normal path takes over (the site's retry queue, which the barrier keeps <b>below</b> the storeys
	/// above). A single worker has no barrier (there is no other floor going up) - only the same 3 short retries.</para>
	/// </summary>
	private static bool TryBodyRetry(CompanionAI ai, State state, string status)
	{
		if (state == null || state.Plan == null || state.Index < 0 || state.Index >= state.Plan.Count
			|| !BuildFailure.IsTransient(status) || !BodyRetryOn())
		{
			return false;
		}
		if (state.Group == null)
		{
			if (state.BodyTries >= BuildFailure.MaxShortRetries)
			{
				return false;
			}
			state.BodyTries++;
			state.Phase = Phase.BodyRetry;
			state.Start = Time.time;
			Log(ai, "um corpo está na peça " + (state.Index + 1) + "/" + state.Plan.Count + " (" + status + ") - tentando de novo em "
				+ BuildFailure.ShortRetryDelay.ToString("F1") + " s (" + state.BodyTries + "/" + BuildFailure.MaxShortRetries + ")");
			return true;
		}
		MHR_BuildGroup.NoteBody(state.Group, state.Index);
		if (!MHR_BuildGroup.TryShortBody(state.Group, state.Index))
		{
			return false;   // the 3 short retries of this piece are used: the site's queue keeps it, below the barrier
		}
		state.Phase = Phase.BodyRetry;
		state.Start = Time.time;
		Log(ai, "um corpo está na peça " + (state.Index + 1) + "/" + state.Plan.Count + " (" + status + ") - tentando de novo em "
			+ BuildFailure.ShortRetryDelay.ToString("F1") + " s (curta " + MHR_BuildGroup.ShortBodyTries(state.Group, state.Index)
			+ "/" + BuildFailure.MaxShortRetries + ")");
		return true;
	}

	/// <summary>11/11/2026: <c>[Building] BodyRetry</c> - the short retries of a spot held by a body (default on).</summary>
	private static bool BodyRetryOn()
	{
		return ModConfig.BuildBodyRetry == null || ModConfig.BuildBodyRetry.Value;
	}

	/// <summary>
	/// 11/11/2026 (Run 10, the <b>companion blocker</b>): the piece was refused with <c>blocked_by_player</c> - say <b>who</b>
	/// is on it with the <b>real numbers</b> of the body (the capsule the probe used, so "+2,0 m over the feet" stops being
	/// a guess). The line is written <b>once per piece</b>: the refusal repeats on each short retry and the numbers are a
	/// measurement, not a watch.
	///
	/// <para>The measured case: the <c>wood_beam</c> of the Run 10 at (-168,1, 41,8, -421,1) held by the Companion 6 at
	/// (-167,1, 39,9, -421,5) - 1,08 m in XZ, 1,9 m below.</para>
	/// </summary>
	private static void BlockedByBody(CompanionAI ai, State state, CompanionSetup cs)
	{
		if (ai == null || state == null || state.Piece == null || state.BodyLogged)
		{
			return;
		}
		Component blocker;
		if (!BuildProbe.BlockerAt(state.Piece, state.Pos, state.Rot, cs, out blocker))
		{
			return;   // the world moved since the check (it was a frame ago): nothing to name
		}
		state.BodyLogged = true;
		if (!CompanionsPlugin.LogNormal)
		{
			return;
		}
		CompanionSetup other = blocker as CompanionSetup;
		string who = blocker is Player ? "você" : (other != null ? "\"" + CompanionNames.NameOf(other) + "\"" : "um corpo");
		Log(ai, "corpo na peça " + (state.Index + 1) + "/" + (state.Plan != null ? state.Plan.Count : state.Index + 1)
			+ " (" + who + ")" + CapsuleText(other != null ? other.GetComponent<Character>() : blocker as Character));
	}

	/// <summary>
	/// 11/11/2026 (Run 10, the <b>companion blocker</b>): the body on the piece is a <b>companion of the same site</b> - ask it
	/// to step aside (<see cref="MHR_BuildGroup.RequestStepAside"/>, <c>[Building] StepAside</c>). That companion answers in
	/// its own wait and walks 2,2 m away from the piece through the mover (<see cref="StepAside"/>); the <b>player is never
	/// moved</b> (the piece waits, exactly as before) and a worker alone has no site to ask. The ask repeats on every
	/// refusal - the request itself is deduplicated by the site (4 s) - while the line is written once per piece.
	///
	/// <para>11/11/2026 (Run 14): the ask only happens when there is <b>somewhere to step to</b> - a spot 2,2 m out that is
	/// clear of the plan's pending pieces and reachable at the companion's level (<see cref="AsideSpot"/> / <see
	/// cref="BuildAsideSpot"/>). Nothing free, or the same pair already shuffling pieces from one piece to the next
	/// (<see cref="MHR_BuildGroup.AsideAsk.Repeating"/>): no ask, the log says why and <b>true</b> comes back - the piece goes
	/// to the site's queue instead of repeating the ping-pong of the ridge rows.</para>
	/// </summary>
	private static bool AskStepAside(CompanionAI ai, State state, CompanionSetup cs)
	{
		if (ai == null || state == null || state.Piece == null || state.Group == null || !StepAsideOn())
		{
			return false;
		}
		Component blocker;
		if (!BuildProbe.BlockerAt(state.Piece, state.Pos, state.Rot, cs, out blocker))
		{
			return false;   // nothing there anymore
		}
		CompanionSetup other = blocker as CompanionSetup;
		if (other == null)
		{
			return false;   // the player (or a creature) is never moved
		}
		CompanionAI mover = other.GetComponent<CompanionAI>();
		if (mover == null || mover == ai)
		{
			return false;
		}
		string who = CompanionNames.NameOf(other);
		Vector3 to;
		if (!AsideSpot(state, ai, mover, state.Pos, out to))
		{
			// 11/11/2026 (Run 14): nowhere free around the piece - do not ask, the piece waits in the queue (the measured
			// ping-pong: the "closest" spot was the next piece of the ridge row, over and over)
			if (!state.AsideLogged && CompanionsPlugin.LogNormal)
			{
				state.AsideLogged = true;
				Log(ai, "\"" + who + "\" está na peça " + (state.Index + 1) + " - sem lugar livre para ele sair da frente, "
					+ "não peço (a peça vai para a fila)");
			}
			return true;
		}
		MHR_BuildGroup.AsideAsk ask = MHR_BuildGroup.RequestStepAside(state.Group, mover.GetInstanceID(),
			ai.GetInstanceID(), state.Index, state.Pos, to);
		if (ask == MHR_BuildGroup.AsideAsk.Repeating)
		{
			// 11/11/2026 (Run 14): the same pair took turns on the piece before this one - stop asking (the piece goes to the
			// queue, the companion walks on with its own work)
			if (!state.AsideLogged && CompanionsPlugin.LogNormal)
			{
				state.AsideLogged = true;
				Log(ai, "\"" + who + "\" saiu da frente da peça anterior e voltou para a peça " + (state.Index + 1)
					+ " - não peço de novo, a peça vai para a fila");
			}
			return true;
		}
		if (ask == MHR_BuildGroup.AsideAsk.Asked)
		{
			// 11/11/2026 (Run 14, the spread of the crew): the number of the acceptance log - only a request the site really
			// stored is an ask (the dedup of 4 s and the anti ping-pong of a pair are not)
			state.AsideAsked++;
			if (!state.AsideLogged && CompanionsPlugin.LogNormal)
			{
				state.AsideLogged = true;
				Log(ai, "\"" + who + "\" está na peça " + (state.Index + 1) + " - pedindo para sair da frente ("
					+ F(to.x) + ", " + F(to.z) + ")");
			}
		}
		return false;
	}

	/// <summary>11/11/2026 (Run 10): the capsule of the body as the probe read it, for the line above
	/// (<c>" - cápsula 39,90-41,70 m, r 0,30"</c>). Empty when there is no body to read.</summary>
	private static string CapsuleText(Character body)
	{
		if (body == null || !BuildProbe.CapsuleOf(body, out _, out float bottom, out float top, out float radius))
		{
			return string.Empty;
		}
		return " - cápsula " + F(bottom) + "-" + F(top) + " m, r " + F(radius);
	}

	/// <summary>11/11/2026 (Run 10): <c>[Building] StepAside</c> - a companion of the site that is only waiting steps out of
	/// the way of a piece another one has to place (default on; off = the old behaviour, the piece waits in the queue).</summary>
	private static bool StepAsideOn()
	{
		return ModConfig.BuildStepAside == null || ModConfig.BuildStepAside.Value;
	}

	/// <summary>How far the waiting companion walks away from the piece (the game's body window needs ~0,3 m of clearance
	/// past the piece's edge; 2,2 m clears the biggest piece of the plan with room for the walk).</summary>
	private const float AsideDistance = 2.2f;

	/// <summary>How long one step aside may take. It is a <b>step</b>: arriving is not required (the mover may be blocked by
	/// the site itself), the worker goes back to its wait and the request is gone.</summary>
	private const float AsideSeconds = 2.5f;

	/// <summary>
	/// 11/11/2026 (Run 10, the companion blocker): the wait of a worker that was asked to move (<c>TakeStepAside</c>). The
	/// <b>spot was chosen by the placer</b> (11/11/2026, Run 14: <see cref="AsideSpot"/> keeps it off the plan's pending
	/// pieces), so this only checks that it is still a place to stand (the ground under it, the same level, reachable) and
	/// walks there with the mover; when the spot is reached, the step expires or there is none, the mover is released and
	/// the normal wait goes on. <b>No teleport and no push</b>: the body is moved by the same movement every other order
	/// uses, so a blocked step is just a step that did not happen.
	///
	/// <para>True while it is still stepping (the caller returns and the normal wait is not asked this frame).</para>
	/// </summary>
	private static bool StepAside(CompanionAI ai, State state, float dt)
	{
		if (state == null || ai == null)
		{
			return false;
		}
		if (state.AsideUntil > 0f)
		{
			if (Time.time < state.AsideUntil && state.AsideTarget != Vector3.zero
				&& CharacterDistance(ai, state.AsideTarget) > 0.4f)
			{
				MHR_Mover.Go(ai, dt, state.AsideTarget, 0.4f, false, "Build");
				return true;
			}
			state.AsideTarget = Vector3.zero;
			state.AsideUntil = 0f;
			MHR_Mover.Release(ai);
			ai.StopMoving();
			return true;   // the step is over: the normal wait starts on the next frame
		}
		if (state.Group == null || !StepAsideOn())
		{
			return false;
		}
		Vector3 from;
		Vector3 to;
		if (!MHR_BuildGroup.TakeStepAside(state.Group, ai.GetInstanceID(), out from, out to))
		{
			return false;
		}
		Vector3 target;
		if (!StepTo(ai.transform.position, to, out target))
		{
			return false;   // the spot is gone (the world changed): stay put, the piece waits in the queue
		}
		state.AsideTarget = target;
		state.AsideUntil = Time.time + AsideSeconds;
		Log(ai, "saindo da frente (peça em " + F(from.x) + ", " + F(from.z) + ") para (" + F(target.x) + ", "
			+ F(target.z) + ")");
		MHR_Mover.Go(ai, dt, target, 0.4f, false, "Build");
		return true;
	}

	/// <summary>
	/// 11/11/2026 (Run 14): the spot the standing companion steps to, chosen by the <b>placer</b> - it is the one that knows
	/// the piece, its own facing and the site's plan. The plan's <b>pending</b> pieces within the reach of the step are
	/// gathered (<c>MHR_BuildGroup.PendingNear</c>) and <see cref="BuildAsideSpot.Plan"/> orders the ring of 8 directions:
	/// away from the placer's front first (the plan's next pieces are in front of it), then its sides, the closest of each
	/// rank first, and <b>nothing within 1,5 m of a pending piece</b>. The first candidate with ground at the companion's
	/// level and a safe step to it wins (<see cref="StepTo"/>); none = no ask at all.
	/// </summary>
	private static bool AsideSpot(State state, CompanionAI ai, CompanionAI mover, Vector3 from, out Vector3 target)
	{
		target = Vector3.zero;
		if (state == null || ai == null || mover == null)
		{
			return false;
		}
		Vector3 feet = mover.transform.position;
		Vector3 facing = ai.transform.forward;   // the placer's front: the piece is in it
		Vector3 away = new Vector3(-facing.x, 0f, -facing.z);
		MHR_BuildGroup.PendingNear(state.Group, from, AsideDistance + BuildAsideSpot.Clearance, s_asidePending);
		int count = BuildAsideSpot.Plan(feet, from, away, s_asidePending, AsideDistance, s_asideSpots);
		for (int i = 0; i < count; i++)
		{
			// 11/11/2026 (Run 14): the spot must not be inside the PLACER either - the first preference points that way
			// (the row behind it is the one already built) and the mover's step check has no characters in its mask, so it
			// would walk into the body that asked it to move. One metre keeps the two capsules apart.
			if (CharacterDistance(ai, s_asideSpots[i]) < 1f)
			{
				continue;
			}
			Vector3 goal;
			if (StepTo(feet, s_asideSpots[i], out goal))
			{
				target = goal;
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// 11/11/2026 (Run 14): is this candidate a place the companion can <b>stand</b>? The ground is looked for straight down
	/// from 1,5 m above it (a ray, so a spot over the void or above a wall is out), it must be within 0,8 m of the
	/// companion's own feet (a step, not a fall) and the way to it must be a safe step (<see cref="MHR_Mover.SafeStep"/>:
	/// no wall, no ledge, no deep water, no fire, no spikes).
	/// </summary>
	private static bool StepTo(Vector3 feet, Vector3 spot, out Vector3 goal)
	{
		goal = Vector3.zero;
		if (!Finite(spot))
		{
			return false;
		}
		RaycastHit hit;
		if (!Physics.Raycast(new Vector3(spot.x, spot.y + 1.5f, spot.z), Vector3.down, out hit, 4f, BuildProbe.PlaceMask(),
			QueryTriggerInteraction.Ignore))
		{
			return false;
		}
		if (Mathf.Abs(hit.point.y - feet.y) > 0.8f)
		{
			return false;   // another level (the deck below, a wall above): not a step, a fall
		}
		Vector3 point = new Vector3(spot.x, hit.point.y, spot.z);
		Vector3 way = point - feet;
		way.y = 0f;
		float flat = way.magnitude;
		if (flat < 0.3f || !MHR_Mover.SafeStep(feet, way / flat, Mathf.Min(flat, 2.5f)))
		{
			return false;
		}
		goal = point;
		return true;
	}

	/// <summary>11/11/2026 (Run 14): are the three coordinates real numbers? (a pose read from a ray or from the plan that
	/// is not a number cannot decide a walk).</summary>
	private static bool Finite(Vector3 v)
	{
		return !float.IsNaN(v.x) && !float.IsInfinity(v.x)
			&& !float.IsNaN(v.y) && !float.IsInfinity(v.y)
			&& !float.IsNaN(v.z) && !float.IsInfinity(v.z);
	}

	/// <summary>11/11/2026 (Run 10): the flat distance from the companion to a point (the "did it arrive" of the step; the
	/// mover's own tolerance is 0,4 m).</summary>
	private static float CharacterDistance(CompanionAI ai, Vector3 point)
	{
		Vector3 delta = point - ai.transform.position;
		delta.y = 0f;
		return delta.magnitude;
	}

	/// <summary>
	/// P1 (04/11/2026, measured on the 985-piece house): a piece refused by the <b>spot</b> (<c>too_steep</c>/<c>no_ground</c>)
	/// gets one chance at the closest pose the game's own rules accept around the plan's one before the normal refusal path
	/// (retry queue / refusal). The piece, its prefab and its plan index do not change - only its <b>working</b> pose (the plan
	/// keeps the original, so a later retry tries it from there again).
	///
	/// <para>How far it may move depends on the piece (<see cref="BuildPieceKind"/>): a <b>structural</b> piece (floor, wall,
	/// roof, beam, pole, stair, door) only up to <b>1 m</b> - the search is asked with that radius and the resulting XZ distance
	/// is checked again, because a wall or a roof row 2,5 m off the plan no longer lines up with the house - while
	/// <b>furniture</b> (a chest, a bed, a fire pit, a workbench) may go to the whole <b>5 m</b> radius: the blueprint's pose
	/// for it is a suggestion. <see cref="BuildSite.Find"/> already answers <c>Ok</c> only for a <c>valid</c> pose. The game's
	/// eye rule is still the limit (<see cref="MaxPlace"/>).</para>
	///
	/// <para>P1 (04/11/2026, second run: two chests <c>piece_chest_wood</c> at (-168,2, 39,9, -434,9) refused with
	/// <c>too_steep</c> - the 2,5 m search found no pose for them): the furniture radius is <b>5 m</b>, and when the 2-ring
	/// pass of the first attempt fails the search is widened to <b>3 rings x 12 directions</b> (both bounded, see
	/// <see cref="NudgeChecks"/>): the site's bank has posts and trees around, and only the wider ring gets past them.</para>
	///
	/// <para>P1 (04/11/2026, second run: the planned <c>piece_workbench</c> at (-178,3, 38,6, -431,7) refused with
	/// <c>clipping</c> at placement): <see cref="PlanStations"/> validates the anchors BEFORE the leveling and the clearing of
	/// the site (<c>StartBlueprint</c>), so what refuses the pose later is the <b>world changing</b> under the plan - a log of
	/// a cut tree rolling onto the anchor, the ground moving. <b>Furniture</b> gets the same chance there (the plan's pose for
	/// a chest or a bench is a suggestion, and the bench covers 20 m, so 5 m does not cost coverage); a <b>structural</b> piece
	/// does not: a wall or a roof off the plan is not the same house, and its clipping is the plan's own overlap. The wide
	/// rings are not walked for a clipping refusal - one pass, the world is what changed.</para>
	/// </summary>
	private static bool NudgePiece(CompanionAI ai, State state, string status)
	{
		if (ai == null || state == null || state.Piece == null)
		{
			return false;
		}
		bool structural = BuildPieceKind.IsStructural(state.Prefab);
		if (structural && status == "clipping")
		{
			return false;   // a structural piece is not moved off the plan for a clipping verdict (the plan overlaps itself)
		}
		float radius = structural ? NudgeStructuralRadius : NudgeFurnitureRadius;
		return AdjustPose(ai, state, status, radius, !structural && status != "clipping");
	}

	/// <summary>
	/// P1 (04/11/2026, measured: 4 pieces of the east gable were given up with <c>falls</c> in the run of the deck): a piece
	/// the companion fell from <b>twice</b> used to be skipped on the spot. Before giving up, ONE attempt at another pose
	/// inside the <b>structural</b> limit (1 m - the same radius a wall/roof gets from <see cref="NudgePiece"/>): the fall
	/// means the way to the pose goes over a hole or past the edge of the build (a gable top above the void), and a metre to
	/// the side is the same house. Once per piece (<see cref="State.FallAdjusted"/>, like the support pull): the fall is about
	/// the WALK, so adjusting it again every two falls would only send him into the same hole. Same index, same claim - only
	/// the working pose changes (the plan keeps the original).
	/// </summary>
	private static bool AdjustFallenPose(CompanionAI ai, State state)
	{
		if (ai == null || state == null || state.Piece == null)
		{
			return false;
		}
		int index = PieceIndex(state);
		if (index < 0 || !state.FallAdjusted.Add(index))
		{
			return false;   // this piece was already adjusted once after a fall
		}
		if (!AdjustPose(ai, state, "falls", NudgeStructuralRadius, false))
		{
			return false;
		}
		// the target moved: the "no progress towards it" clock of the walk restarts from here
		state.BestDistance = 0f;
		state.BestAt = Time.time;
		return true;
	}

	/// <summary>
	/// P1 (04/11/2026): the shared half of <see cref="NudgePiece"/> and <see cref="AdjustFallenPose"/> - the closest pose the
	/// game's own rules accept around the plan's one (<see cref="BuildSite.Find"/>, <paramref name="radius"/> metres, 2 rings
	/// x 8 directions bounded by <see cref="NudgeChecks"/>), the XZ cap of the radius itself (a piece moved further is not the
	/// same piece), the game's eye rule (<see cref="MaxPlace"/>) and the line that says what moved. A refused search returns
	/// false and the caller keeps its normal path.
	/// </summary>
	private static bool AdjustPose(CompanionAI ai, State state, string status, float radius, bool wide)
	{
		Vector3 forward = state.Forward.sqrMagnitude > 0.01f ? state.Forward : ai.transform.forward;
		// lote 1: the same station rule as the rest of the order (a waived blueprint must not lose the pose to needs_station)
		bool stationNeeded = StationRequired(state);
		BuildSiteResult site = BuildSite.Find(state.Piece, state.Pos, forward, radius, 2, 8, NudgeChecks, Self(ai), stationNeeded);
		if ((site == null || !site.Ok) && wide)
		{
			// the furniture of a bank does not fit inside 2,5 m: walk the wider rings once before losing the piece
			site = BuildSite.Find(state.Piece, state.Pos, forward, radius, 3, 12, NudgeChecksWide, Self(ai), stationNeeded);
		}
		if (site == null || !site.Ok)
		{
			return false;   // no pose of the game's rules accepted: the retry queue / refusal takes over
		}
		float dx = site.Pos.x - state.Pos.x;
		float dz = site.Pos.z - state.Pos.z;
		if (dx * dx + dz * dz > radius * radius)
		{
			return false;   // a structural piece 1 m off the plan is not the same house (a 5 m furniture pose is already capped)
		}
		Vector3 me = ai.transform.position;
		Vector3 eye = new Vector3(me.x, me.y + EyeHeight, me.z);
		float flat = new Vector2(me.x - site.Pos.x, me.z - site.Pos.z).magnitude;
		if (flat > MaxPlace || Vector3.Distance(eye, site.Pos) > MaxPlace)
		{
			return false;   // the closest free pose is out of the eye rule from where it stands
		}
		Log(ai, "log ajustado: " + state.Prefab + " de (" + F(state.Pos.x) + ", " + F(state.Pos.y) + ", " + F(state.Pos.z)
			+ ") para (" + F(site.Pos.x) + ", " + F(site.Pos.y) + ", " + F(site.Pos.z) + ") - " + status);
		state.Pos = site.Pos;
		state.Rot = site.Rot;
		state.Checked = false;
		return true;
	}

	private static bool Verify(CompanionAI ai, State state, CompanionSetup cs)
	{
		long creator = BuildExecutor.CreatorOf(cs, Player.m_localPlayer);
		// Lote 1 (11/11/2026): with the stations waived in a blueprint the registry check is waived too - otherwise the
		// piece is placed and comes back to the retry queue forever (station_not_registered), since nothing is planned
		// to register it.
		bool checkStation = StationRequired(state);
		string reason = state.Placed != null
			? BuildExecutor.Verify(state.Placed, state.Piece, creator, checkStation)
			: BuildExecutor.VerifyAt(state.Piece, state.Pos, creator, out state.Placed, checkStation);
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
		CountPlaced(state);   // P1 (review A7): one mark per plan index, counted once
		// E7 (player's requests, 03/11/2026: "he has to know he should place the piece at the top of the ladder and build
		// the floor there" + "he can build the floor from the bottom up too, he does not need to wait until he is at the floor
		// height"): a floor that lands on what was just placed comes to the front of the plan, so the upper floor is built
		// from the level he is standing on instead of waiting for the whole storey below to finish
		bool isLadder = state.Prefab != null && state.Prefab.IndexOf("ladder", StringComparison.OrdinalIgnoreCase) >= 0;
		// E7 (player's report, 03/11/2026: "the site is too ugly"): ONLY the ladder pulls a floor forward. Generalising it to
		// every support (wall/beam/pole) pulled a floor 2 m above each of them, which filled the site with floating floors.
		float above = isLadder ? LadderTop(state) : float.NaN;
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
			Group = state.Group,
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
	/// P1 (11/11/2026, the measured 95 refusals of the emergency post): the pose was refused <b>only</b> by the support rule -
	/// one reason, <c>no_support</c>. With a base the ray found and that <b>holds</b> (a floor below, a beam top) the post is
	/// planted anyway: our own support prediction is the conservative one (<see cref="BuildProbe"/> reads the piece under the
	/// <b>centre</b> and its own footprint box), while the game connects the post to the base by <b>collider overlap</b>. If the
	/// post does fall, the piece it was meant to hold fails the game's own verdict on the next frame (<see cref="BuildSupport"/>
	/// runs after the placement) and follows the normal retry/`falls` path - no special case. Anything else in the list -
	/// <c>clipping</c>, <c>needs_station</c>, <c>ward</c>, <c>blocked_by_player</c>, <c>too_steep</c> - is a real refusal and
	/// keeps the post out.
	/// </summary>
	private static bool OnlyNoSupport(Dictionary<string, object> check)
	{
		object raw = null;
		List<string> reasons = check != null && check.TryGetValue("reasons", out raw) ? raw as List<string> : null;
		return reasons != null && reasons.Count == 1 && reasons[0] == "no_support";
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
					// P1 (multi-build): the body that came back goes to its site again (its piece may be another's now -
					// RejoinGroup claims again and moves on when it is taken or already built)
					RejoinGroup(ai, fresh, pending.Group);
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
		ResumePending(ai, cs, pending);
	}

	/// <summary>
	/// E6b (player's test, 02/11/2026): the materials are in - the SAME order comes back at the same anchor and from the
	/// same piece. It was the tail of <see cref="CheckPending"/> and it is a method of its own because the build panel's
	/// "resume" runs exactly this path (a paused order waiting for materials), so a resume after a pause cannot drift
	/// from a resume after a gather.
	/// </summary>
	private static void ResumePending(CompanionAI ai, CompanionSetup cs, Pending pending)
	{
		if (ai == null || pending == null)
		{
			return;
		}
		// the very two resolutions CheckPending made before deciding to resume (the caller's template/blueprint are not
		// passed: the pending is the source of truth, and a resume may come from the panel much later)
		BuildTemplates.Template template = pending.Template != null ? BuildTemplates.ByName(pending.Template) : null;
		Blueprint blueprint = pending.BlueprintFile != null ? BuildBlueprints.Load(pending.BlueprintFile) : null;
		Humanoid humanoid = ai.GetComponent<Humanoid>();
		Inventory bag = humanoid != null ? humanoid.GetInventory() : null;
		bool nearlyFull = bag != null && humanoid != null && bag.GetTotalWeight() >= CompanionCarry.Max(humanoid) * 0.8f;
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
			// P1 (multi-build): the gather is over - the companion goes back to its site (if the site is still alive)
			if (s_state.TryGetValue(ai.GetInstanceID(), out fresh) && fresh != null)
			{
				RejoinGroup(ai, fresh, pending.Group);
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
		// P1 (multi-build): the worker leaves the site - its piece is free for the others right away, and the group is
		// dropped when the last one leaves (the plan's pieces that are left are lost with it, like a cancelled order)
		if (state.Group != null)
		{
			MHR_BuildGroup.ReleaseAllOf(state.Group, ai.GetInstanceID());
			MHR_BuildGroup.Leave(state.Group, ai.GetInstanceID());
			state.Group = null;
		}
		// 11/11/2026 (Run 14, the spread of the crew): the three numbers of the order in ONE line - what the acceptance run
		// reads (the Run 14 log had to be grepped line by line for "corpo na peça" and "pedindo para sair da frente").
		// 11/11/2026 (Run 17): the same line carries the WAIT - the seconds the crew stood still and why (the barrier with a
		// body x a dependency, the site's queue, the step aside). It is what makes the A/B of the body barrier honest
		// instead of a guess.
		if (state.SpreadSkipped > 0 || state.SpreadResumed > 0 || state.AsideAsked > 0
			|| state.WaitBody > 0f || state.WaitDependency > 0f || state.WaitQueueEnd > 0f || state.WaitAhead > 0f
			|| state.WaitGate > 0f || state.WaitAside > 0f
			|| state.BodyHolds > 0 || state.BarrierHolds > 0)
		{
			// 11/11/2026 (Run 18): the queue split by where the walk is (the END of the plan x the MIDDLE of it) and the
			// gate's own seconds - the gate ran with dt = 0 and fell in no bucket, and a good part of the "fila" of the
			// Run 17 measure was a worker held by it with a free piece right ahead.
			float barrier = state.WaitBody + state.WaitDependency;
			float queue = state.WaitQueueEnd + state.WaitAhead;
			float total = barrier + queue + state.WaitAside + state.WaitGate;
			Log(ai, "dispersão: " + state.SpreadSkipped + " adiada(s), " + state.SpreadResumed + " retomada(s) · step-aside: "
				+ state.AsideAsked + " pedido(s) · espera: " + F(total) + " s (barreira " + F(barrier) + " s em "
				+ state.BodyHolds + " retenção(ões) " + F(state.WaitBody) + " corpo/" + F(state.WaitDependency)
				+ " dependência, fila " + F(queue) + " s (fim " + F(state.WaitQueueEnd) + " s, meio "
				+ F(state.WaitAhead) + " s), passo " + F(state.WaitAside) + " s, portão "
				+ state.BarrierHolds + " (" + F(state.WaitGate) + " s))");
		}
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

	/// <summary>11/11/2026 (Run 14, the spread of the crew): a detail line - only with <c>[Advanced] LogLevel = Detailed</c>.
	/// The spread happens on most pieces of a shared plan, so it is not Normal-level news (the one summary line of
	/// <see cref="Finish"/> is).</summary>
	private static void LogDetailed(CompanionAI ai, string message)
	{
		if (!CompanionsPlugin.LogDetailed)
		{
			return;
		}
		if (CompanionsPlugin.Log != null)
		{
			CompanionsPlugin.Log.LogDebug(LogTag + Who(ai) + message);
		}
	}
}
