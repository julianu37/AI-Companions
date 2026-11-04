using System.Collections.Generic;
using UnityEngine;

namespace Companions;

/// <summary>
/// P1 (multi-build, 03/11/2026): the <b>build group</b> - several companions on the <b>same</b> plan (one blueprint,
/// distinct pieces, nothing built twice). The group owns the exact plan the leader resolved
/// (<see cref="MHR_Build.PlannedStep"/>), the <b>claims</b> (one piece belongs to one worker at a time) and the
/// <b>retry queue</b> (the pieces that did not stand yet, which any worker may pull again).
///
/// <code>
/// leader     MHR_Build.StartBlueprint (level, clear, PlanStations) -> the plan
/// group      this file -> the plan + the claims + the retry queue + the BuildPhase flag
/// followers  MHR_Build.StartBlueprint(storedPlan: the group plan) -> Phase.WaitGroup
/// drive      TryClaim(index) -> walk -> pay -> place -> Verify -> Advance (Release)
/// end        Advance -> NextRetry (a free index of the queue) -> WaitRetry -> Finish
/// </code>
///
/// <para>Rules: <b>main thread only</b> (claims and verdicts are per frame, so there is no lock and the scratch buffer is
/// a field); the group lives in memory like the order itself (it is not in the save); the group does <b>not</b> die with
/// a worker (a dead one leaves and the others go on) and it is dropped when the last one leaves. The site work
/// (leveling/clearing) is the <b>leader's</b> alone - <see cref="BuildPhase"/> gates the others - and when the leader
/// leaves the next worker is promoted (<see cref="LeaderOf"/>): it is the one that inserts a workbench in the middle of
/// the plan and the one that may reorder it (with a single worker on the site).</para>
///
/// <para><b>P1 MVP (limitations):</b> there is no per-piece <b>material reservation</b> between the workers (each one
/// checks the shortfall of the piece it is on with its own bag + the site's chests, so the same stack may be counted
/// twice and the second one hands the order to the gather). The <c>Stock</c> of <c>docs/plan-multi-build.md</c> §3 is
/// the open half of P1.</para>
/// </summary>
internal static class MHR_BuildGroup
{
	/// <summary>P1: how long a claim holds its piece without being touched again. A live worker refreshes it every frame
	/// (<see cref="Refresh"/>); the expiry is what frees the pieces of a worker whose game went away.</summary>
	internal const float ClaimSeconds = 20f;

	/// <summary>P1 (review A1, 03/11/2026): how long a piece that just failed waits before it may be pulled again - the
	/// neighbours it needs may be going up right now (it is not retried on the same breath).</summary>
	internal const float RetryDelay = 2f;

	/// <summary>11/11/2026 (Run 10): how long a <b>step-aside</b> request lives. The companion answers it in its own wait;
	/// one that is not answered inside this window (it was walking to its own piece, or the refusal was settled) expires,
	/// so nobody is moved for a piece that is no longer being placed.</summary>
	internal const float StepAsideSeconds = 5f;

	/// <summary>11/11/2026 (Run 10): the same piece refused by the same body repeats the request every frame of a retry -
	/// one ask per this many seconds is enough (and the companion walking away takes a moment).</summary>
	internal const float StepAsideRepeat = 4f;

	/// <summary>P1 (review A1): one piece of the plan that did not stand yet. It stays in the queue until the piece is
	/// done or runs out of <b>real</b> attempts (<c>MHR_Build.MaxPasses</c>).</summary>
	private sealed class RetryEntry
	{
		internal int Index;
		/// <summary>How many times it was queued after a real failure (the cap is per index, shared by the whole site).
		/// A failure by <b>dependency</b> (the piece below or the workbench is not up yet) does not come here.</summary>
		internal int Attempts;
		/// <summary>How many times it was deferred for lack of a dependency - the wait grows with this number
		/// (8 s, 12 s, 20 s, then 25 s) and it is never a reason to give the piece up.</summary>
		internal int Dependency;
		/// <summary>11/11/2026 (the storey barrier): how many times it was refused because a <b>body</b> was on the spot. It
		/// never spends an attempt (nobody's fault) and it is not a dependency either: the wait is the short, fixed
		/// <c>BuildFailure.BodyRetryDelay</c>.</summary>
		internal int Blocked;
		/// <summary>When it may be pulled again.</summary>
		internal float At;
	}

	/// <summary>P1: one site - the plan of the leader, its workers, the pieces they hold and what is left to retry.</summary>
	private sealed class Group
	{
		internal string Key;
		internal string Label;
		/// <summary>OwnerHash of the player the group belongs to (other players never join).</summary>
		internal long Owner;
		/// <summary>The EXACT plan of the leader - the followers build from this same list, index by index.</summary>
		internal List<MHR_Build.PlannedStep> Plan;
		/// <summary>The leader finished the site (leveling/clearing) - the followers may build.</summary>
		internal bool BuildPhase;
		internal int LeaderId;
		internal readonly List<int> Workers = new List<int>();
		/// <summary>Plan index -> the worker that owns the piece.</summary>
		internal readonly Dictionary<int, int> Claims = new Dictionary<int, int>();
		/// <summary>Plan index -> when its claim was last touched (the <see cref="ClaimSeconds"/> expiry).</summary>
		internal readonly Dictionary<int, float> ClaimAt = new Dictionary<int, float>();
		/// <summary>P1 (review A7): the plan indices that are DONE for the site - every piece is counted once.</summary>
		internal readonly HashSet<int> Done = new HashSet<int>();
		/// <summary>P1 (04/11/2026, "contadores honestos"): the plan indices that were given up - a SET, so a piece that
		/// failed four times is one skipped piece and not four. Disjoint from <see cref="Done"/>.</summary>
		internal readonly HashSet<int> SkippedIndex = new HashSet<int>();
		/// <summary>P1 (review A1): the pieces that did not stand yet - the site's queue, never a worker's.</summary>
		internal readonly List<RetryEntry> Retry = new List<RetryEntry>();
		/// <summary>P1 (04/11/2026): when a piece of this site last went up - the stall watchdog counts from here (not
		/// from the moment a worker entered a wait, which says nothing about the site moving).</summary>
		internal float LastProgressAt;
		/// <summary>
		/// 11/11/2026 (the <b>body barrier</b>): plan index -> when a <b>body</b> (the player walking over the spot) first
		/// refused it. The entry is the barrier: while it is younger than <c>BuildFailure.BodyBarrierSeconds</c>, the pieces
		/// of the storeys above that one are not taken (<see cref="MayTake"/>). The time is written <b>once</b> per index
		/// (<see cref="NoteBody"/> is idempotent) - a player who keeps walking over the spot does not hold the site forever.
		/// </summary>
		internal readonly Dictionary<int, float> BodyAt = new Dictionary<int, float>();
		/// <summary>11/11/2026 (the body barrier): plan index -> how many SHORT retries it already had for a body. Counted
		/// <b>for the site</b> (never per worker: a piece must not get 3 per companion) and capped by
		/// <c>BuildFailure.MaxShortRetries</c>.</summary>
		internal readonly Dictionary<int, int> BodyTries = new Dictionary<int, int>();
		/// <summary>
		/// 11/11/2026 (Run 10, the companion blocker): worker id -> the request that this companion <b>step aside</b> from a
		/// piece another one is placing. It is asked by the placer when the body on the piece is a companion of the site and
		/// consumed by that companion's own wait (<c>MHR_Build.WaitRetry</c>) - nobody moves a body of another order, and
		/// the player is never asked (the game's ghost check refuses the spot and the piece waits).
		/// </summary>
		internal readonly Dictionary<int, StepAside> Aside = new Dictionary<int, StepAside>();
	}

	/// <summary>11/11/2026 (Run 10): "please move, you are standing on the piece I have to place" - the pose to walk away
	/// from and when it was asked.</summary>
	private sealed class StepAside
	{
		/// <summary>The pose of the piece the other worker is placing (the point to get away from).</summary>
		internal Vector3 From;
		/// <summary>When it was asked - the dedup of <see cref="RequestStepAside"/> and the expiry of
		/// <see cref="TakeStepAside"/> count from here.</summary>
		internal float At;
	}

	private static readonly Dictionary<string, Group> s_groups = new Dictionary<string, Group>();

	/// <summary>Worker id -> group key. It is what makes <c>MHR_Build.Active</c> answer for the group in O(1) (that
	/// guard is asked by every job, every frame).</summary>
	private static readonly Dictionary<int, string> s_member = new Dictionary<int, string>();

	/// <summary>P1 (review A12, 03/11/2026): scratch buffer of the pruning paths - the hot path must not allocate a list
	/// per call (main thread only, cleared before and after each use).</summary>
	private static readonly List<int> s_drop = new List<int>();

	/// <summary>
	/// P1: the key of a site - the label of the blueprint/template, the anchor rounded to 1 m, the yaw rounded to 5
	/// degrees and the owner. Two companions that aim at the same spot land on the same key (the rounding absorbs the
	/// player's hand); another player's group is never joined.
	/// </summary>
	internal static string KeyOf(string label, Vector3 anchor, Quaternion rot, long owner)
	{
		int x = Mathf.RoundToInt(anchor.x);
		int y = Mathf.RoundToInt(anchor.y);
		int z = Mathf.RoundToInt(anchor.z);
		int yaw = Mathf.RoundToInt(rot.eulerAngles.y / 5f) * 5;
		if (yaw >= 360)
		{
			yaw -= 360;
		}
		return (label ?? "?") + "|" + x + "," + y + "," + z + "|" + yaw + "|" + owner;
	}

	/// <summary>
	/// P1: one site per owner at a time (§3 of the plan): a second build order while a group is running would make two
	/// leaders over the same ground (or the same plan with new indices), so it is refused and the player says "para"
	/// first. It is asked BEFORE the leader starts anything.
	/// </summary>
	internal static bool SiteBusy(long owner)
	{
		foreach (KeyValuePair<string, Group> kv in s_groups)
		{
			if (kv.Value != null && kv.Value.Owner == owner)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>P1: creates the group of a site and puts the leader in it. False when the key is already taken (the
	/// caller then keeps the order it has).</summary>
	internal static bool TryCreate(string key, string label, long owner, List<MHR_Build.PlannedStep> plan, int leaderId)
	{
		if (string.IsNullOrEmpty(key))
		{
			return false;
		}
		Group existing;
		if (s_groups.TryGetValue(key, out existing) && existing != null)
		{
			return false;
		}
		Group group = new Group
		{
			Key = key,
			Label = label ?? "?",
			Owner = owner,
			Plan = plan,
			LeaderId = leaderId,
			LastProgressAt = Time.time
		};
		s_groups[key] = group;
		Join(group, leaderId);
		return true;
	}

	/// <summary>P1: puts a companion in a running group. False when the group is gone.</summary>
	internal static bool TryJoin(string key, int workerId)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null)
		{
			return false;
		}
		Join(group, workerId);
		return true;
	}

	private static void Join(Group group, int workerId)
	{
		if (group == null || workerId == 0 || group.Workers.Contains(workerId))
		{
			return;
		}
		group.Workers.Add(workerId);
		s_member[workerId] = group.Key;
	}

	/// <summary>P1: takes a worker out of the group and frees its pieces. True when it was the <b>last</b> one (the group
	/// is gone).</summary>
	internal static bool Leave(string key, int workerId)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null)
		{
			return false;
		}
		ReleaseAll(group, workerId);
		group.Workers.Remove(workerId);
		string mapped;
		if (s_member.TryGetValue(workerId, out mapped) && mapped == key)
		{
			s_member.Remove(workerId);
		}
		if (group.Workers.Count == 0)
		{
			s_groups.Remove(key);
			return true;
		}
		if (group.LeaderId == workerId)
		{
			// P1 (review A2, 03/11/2026): the leader left (a gather, a death, "para, Fulano") and the SITE CANNOT stay
			// without one - the next worker alive takes it (it is the one that inserts a bench and may reorder the plan)
			bool siteWork = !group.BuildPhase;
			if (siteWork)
			{
				// it left before finishing the site work: the others go on with the site as it is (the pieces that do not
				// stand are retried)
				group.BuildPhase = true;
			}
			group.LeaderId = group.Workers[0];
			// P1 (review B3, 03/11/2026): the normal end of an order is the leader finishing its plan and leaving - that is
			// not news. The promotion is said out loud only when there is still site work or a retry queue to walk; the
			// other cases are a detail line (and the string is only built when it will be written).
			if (siteWork || group.Retry.Count > 0)
			{
				Log("o líder do grupo \"" + group.Label + "\" saiu - outro companheiro assumiu o canteiro");
			}
			else if (CompanionsPlugin.LogDetailed)
			{
				LogDebug("o líder do grupo \"" + group.Label + "\" saiu - outro companheiro assumiu o canteiro");
			}
		}
		return false;
	}

	/// <summary>P1: drops every group ("para" without a name, scene load, shutdown). The pieces already placed stay,
	/// exactly like a cancelled order.</summary>
	internal static void Clear()
	{
		s_groups.Clear();
		s_member.Clear();
	}

	internal static void CancelAll()
	{
		if (s_groups.Count == 0)
		{
			return;
		}
		// P1 (review B4, 03/11/2026): the line is built inside the gate - this runs on every "para" and scene load
		if (CompanionsPlugin.LogNormal)
		{
			Log("a ordem sem alvo derrubou " + s_groups.Count + " grupo(s) de obra (" + s_member.Count + " companheiro(s))");
		}
		Clear();
	}

	internal static bool Exists(string key)
	{
		Group group;
		return !string.IsNullOrEmpty(key) && s_groups.TryGetValue(key, out group) && group != null;
	}

	/// <summary>P1: this companion is a member of some group (the answer of <c>MHR_Build.Active</c> for a body whose
	/// state is on another frame).</summary>
	internal static bool Worker(int workerId)
	{
		return s_member.ContainsKey(workerId);
	}

	/// <summary>P1 (review A2, 03/11/2026): is this worker the <b>site's</b> leader right now? The leader is the group's
	/// (it is reassigned when the old one leaves), so a state's own flag would lie. Without a group every worker is its
	/// own leader, which keeps the single-worker order of 03/11/2026 unchanged.</summary>
	internal static bool IsLeader(string key, int workerId)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null)
		{
			return true;
		}
		return group.LeaderId == workerId;
	}

	/// <summary>P1: the site is ready (the leader leveled/cleared it) - the followers may build.</summary>
	internal static bool BuildPhase(string key)
	{
		Group group;
		return !string.IsNullOrEmpty(key) && s_groups.TryGetValue(key, out group) && group != null && group.BuildPhase;
	}

	internal static void SetBuildPhase(string key, bool on)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null)
		{
			return;
		}
		group.BuildPhase = on;
	}

	/// <summary>
	/// P1: the piece at <paramref name="index"/> becomes this worker's. False when another worker holds it (and the claim
	/// has not expired) - the caller skips the piece (the index moves on, nothing is paid, it is not counted as done).
	/// Without a group it always answers true, so the single-worker path is unchanged.
	/// </summary>
	internal static bool TryClaim(string key, int index, int workerId)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null)
		{
			return true;
		}
		return TryClaim(group, index, workerId);
	}

	private static bool TryClaim(Group group, int index, int workerId)
	{
		int holder;
		float at;
		if (group.Claims.TryGetValue(index, out holder) && holder != workerId
			&& group.ClaimAt.TryGetValue(index, out at) && Time.time - at < ClaimSeconds)
		{
			return false;
		}
		group.Claims[index] = workerId;
		group.ClaimAt[index] = Time.time;
		return true;
	}

	/// <summary>P1: the worker that owns the piece right now (0 when nobody does, or when the claim expired).</summary>
	internal static int OwnerOf(string key, int index)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null)
		{
			return 0;
		}
		int holder;
		float at;
		if (!group.Claims.TryGetValue(index, out holder) || !group.ClaimAt.TryGetValue(index, out at)
			|| Time.time - at >= ClaimSeconds)
		{
			return 0;
		}
		return holder;
	}

	/// <summary>P1: keeps the piece alive while the worker walks to it (the 20 s expiry is for a worker that went
	/// away, not for a long walk).</summary>
	internal static void Refresh(string key, int index, int workerId)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null)
		{
			return;
		}
		int holder;
		if (group.Claims.TryGetValue(index, out holder) && holder == workerId)
		{
			group.ClaimAt[index] = Time.time;
		}
	}

	/// <summary>P1: frees one piece (placed, failed, cancelled). Only the owner frees it.</summary>
	internal static void Release(string key, int index, int workerId)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null)
		{
			return;
		}
		int holder;
		if (group.Claims.TryGetValue(index, out holder) && holder == workerId)
		{
			group.Claims.Remove(index);
			group.ClaimAt.Remove(index);
		}
	}

	/// <summary>P1: frees everything this worker holds (it holds one piece at a time - this is also the housekeeping
	/// that drops the claims that expired).</summary>
	internal static void ReleaseAllOf(string key, int workerId)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null)
		{
			return;
		}
		ReleaseAll(group, workerId);
	}

	private static void ReleaseAll(Group group, int workerId)
	{
		if (group == null || group.Claims.Count == 0)
		{
			return;
		}
		float now = Time.time;
		s_drop.Clear();
		foreach (KeyValuePair<int, int> kv in group.Claims)
		{
			float at;
			bool stale = !group.ClaimAt.TryGetValue(kv.Key, out at) || now - at >= ClaimSeconds;
			if (kv.Value == workerId || stale)
			{
				s_drop.Add(kv.Key);
			}
		}
		for (int i = 0; i < s_drop.Count; i++)
		{
			group.Claims.Remove(s_drop[i]);
			group.ClaimAt.Remove(s_drop[i]);
		}
		s_drop.Clear();
	}

	/// <summary>P1: the plan of the group (the very list the leader built and every worker walks by index).</summary>
	internal static List<MHR_Build.PlannedStep> Plan(string key)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null)
		{
			return null;
		}
		return group.Plan;
	}

	/// <summary>
	/// P1 (review B1, 03/11/2026): this piece of the plan is <b>done</b> for the site (it was paid and placed). The
	/// workers walk the plan by <b>index</b>, and a retry may be an index <b>behind</b> the current one: after building it
	/// the tail of the plan must not be walked again - the pieces that already stand are skipped (no claim, no walking).
	/// Without this the retry re-walks the tail and either pays for a duplicate (a piece without <c>m_noClipping</c>
	/// passes the ghost check) or burns its attempts on <c>clipping</c>.
	/// </summary>
	internal static bool IsDone(string key, int index)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null)
		{
			return false;
		}
		return group.Done.Contains(index);
	}

	/// <summary>
	/// P1 (review B1, 03/11/2026): this piece already failed <b>for the site</b>, so a copy of it may be standing (a piece
	/// that was placed and then lost its support stays in the world) - the caller asks the pose before paying again
	/// instead of placing a second one on top of it. P1 (04/11/2026): a piece deferred by <b>dependency</b> counts here too
	/// (it was tried and did not stand; with the support gate it spends no attempt, so <c>Attempts</c> alone would miss it).
	/// 11/11/2026: a piece a <b>body</b> held counts too (<c>Blocked</c>) - it was tried, nothing was paid and it may have
	/// gone up in another worker's hands.
	/// </summary>
	internal static bool Retried(string key, int index)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null)
		{
			return false;
		}
		for (int i = 0; i < group.Retry.Count; i++)
		{
			RetryEntry entry = group.Retry[i];
			if (entry != null && entry.Index == index && (entry.Attempts > 0 || entry.Dependency > 0 || entry.Blocked > 0))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// 11/11/2026 (the <b>emergency post</b> when the plan's own support does not come, Run 9): the piece of this index was
	/// <b>given up</b> for the site (the retry queue ran out of real attempts, or the stall watchdog dropped it) - so the
	/// support the plan has under a refused piece is never coming and the post may take over
	/// (<c>BuildSupportBelow.FallbackPostAllowed</c>). False without a group: the single-worker path keeps its own
	/// <c>SkippedIndices</c>.
	/// </summary>
	internal static bool GivenUp(string key, int index)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null || index < 0)
		{
			return false;
		}
		return group.SkippedIndex.Contains(index);
	}

	/// <summary>
	/// 11/11/2026 (the emergency post): how many times this piece of the plan was deferred for lack of a <b>dependency</b> -
	/// the clock the plan had to deliver what holds it up. 0 when it is not in the queue (never deferred).
	/// </summary>
	internal static int DependencyOf(string key, int index)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null)
		{
			return 0;
		}
		for (int i = 0; i < group.Retry.Count; i++)
		{
			RetryEntry entry = group.Retry[i];
			if (entry != null && entry.Index == index)
			{
				return entry.Dependency;
			}
		}
		return 0;
	}

	/// <summary>
	/// 11/11/2026 (the <b>body barrier</b> of the walk order): a piece was refused because a <b>body</b> - the player
	/// walking over the spot - is on it. The <b>first</b> refusal of a plan index is what starts the barrier
	/// (<c>BuildFailure.BodyBarrierSeconds</c>): the write is idempotent, so the clock is never pushed forward and a
	/// player who parks on the spot cannot hold the site forever. Measured reason (Run 9, 11/11/2026): the entrance
	/// steps (pieces 8-10 of 988) were refused by the body, went to the site's queue and only came back at the very end,
	/// after the whole house - the player's rule is "they may skip, but come back and try again before going up".
	/// </summary>
	internal static void NoteBody(string key, int index)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null || index < 0)
		{
			return;
		}
		if (group.BodyAt.ContainsKey(index))
		{
			return;   // the barrier of this piece is already drawn - the first refusal is the clock
		}
		group.BodyAt[index] = Time.time;
		float level = group.Plan != null && index < group.Plan.Count ? group.Plan[index].Level : 0f;
		Log("um corpo está na peça " + (index + 1) + " do canteiro (nível " + level.ToString("F1")
			+ ") - nada acima de " + (level + BuildFailure.BodySameStorey).ToString("F1") + " m é pego por "
			+ BuildFailure.BodyBarrierSeconds.ToString("F0") + " s");
	}

	/// <summary>
	/// 11/11/2026 (the body barrier): may this piece be tried again <b>right away</b> at the same pose? Three short
	/// retries per plan index for the <b>whole site</b> (a piece must not get 3 per companion) - they spend none of the
	/// 3 real attempts. False when they are used up: the piece takes the normal path (the site's queue, which the
	/// barrier keeps below the storeys above).
	/// </summary>
	internal static bool TryShortBody(string key, int index)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null || index < 0)
		{
			return false;
		}
		int tries;
		group.BodyTries.TryGetValue(index, out tries);
		if (!BuildFailure.ShortRetry("blocked_by_player", tries))
		{
			return false;
		}
		group.BodyTries[index] = tries + 1;
		return true;
	}

	/// <summary>11/11/2026 (the body barrier): how many short retries this piece already had for a body (for the log).</summary>
	internal static int ShortBodyTries(string key, int index)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null)
		{
			return 0;
		}
		int tries;
		return group.BodyTries.TryGetValue(index, out tries) ? tries : 0;
	}

	/// <summary>
	/// 11/11/2026 (the body barrier, the player's rule): may the site take this piece <b>now</b>? A piece is held back
	/// only while a <b>barrier is alive</b> (a body refused one of the plan's pieces less than
	/// <c>BuildFailure.BodyBarrierSeconds</c> ago): the pieces of the storeys <b>above</b> the blocked one
	/// (<c>BuildFailure.BodySameStorey</c> = 1,5 m) wait for it, the ones of the same storey do not. The blocked piece
	/// itself is never held back by its own barrier - it is exactly the one that has to be retried first.
	///
	/// <para>Without a group (a single worker, a template) it always answers true: there is no other floor going up.</para>
	/// </summary>
	internal static bool MayTake(string key, int index)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null)
		{
			return true;
		}
		return MayTake(group, index);
	}

	private static bool MayTake(Group group, int index)
	{
		if (group.BodyAt.Count == 0 || group.Plan == null || index < 0 || index >= group.Plan.Count)
		{
			return true;
		}
		float now = Time.time;
		float level = group.Plan[index].Level;
		foreach (KeyValuePair<int, float> barrier in group.BodyAt)
		{
			if (now - barrier.Value >= BuildFailure.BodyBarrierSeconds || barrier.Key < 0 || barrier.Key >= group.Plan.Count)
			{
				continue;
			}
			if (level > group.Plan[barrier.Key].Level + BuildFailure.BodySameStorey)
			{
				return false;   // a storey above what a body is holding
			}
		}
		return true;
	}

	/// <summary>11/11/2026 (the body barrier): the barriers of the site are dropped with the queue - the stall watchdog's
	/// verdict is "nothing is moving", and a barrier that is only <b>waiting</b> for a body must not bring the order back
	/// to the wait (and the watchdog) on the next frame. The step-aside requests go with them (nobody is going to move for
	/// a queue that was given up).</summary>
	internal static void ClearBodies(string key)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null)
		{
			return;
		}
		group.BodyAt.Clear();
		group.BodyTries.Clear();
		group.Aside.Clear();
	}

	/// <summary>
	/// 11/11/2026 (Run 10, the <b>companion blocker</b>): a companion of the site is standing on the piece another one has to
	/// place - ask it to step out of the way. Only a live site answers, and only for a <b>worker of that site</b> (a worker
	/// alone has nobody to move, and a request outside a group would let the build steer a body that belongs to another
	/// order). A repeat within <see cref="StepAsideRepeat"/> seconds is dropped: the refusal repeats every frame of a retry,
	/// and one request is enough.
	/// </summary>
	internal static void RequestStepAside(string key, int workerId, Vector3 from)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null || workerId == 0
			|| !group.Workers.Contains(workerId))
		{
			return;   // only a worker of THIS site: nobody else would answer, and the request would just sit there
		}
		StepAside entry;
		if (group.Aside.TryGetValue(workerId, out entry) && entry != null && Time.time - entry.At < StepAsideRepeat)
		{
			return;
		}
		group.Aside[workerId] = new StepAside { From = from, At = Time.time };
	}

	/// <summary>
	/// 11/11/2026 (Run 10): the request this worker has to answer, consumed <b>once</b> (the worker walks away and comes
	/// back to its wait). False when it has none, or when the request is older than <see cref="StepAsideSeconds"/> - the
	/// piece it was about was settled (or given up) in the meantime.
	/// </summary>
	internal static bool TakeStepAside(string key, int workerId, out Vector3 from)
	{
		from = Vector3.zero;
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null || workerId == 0)
		{
			return false;
		}
		StepAside entry;
		if (!group.Aside.TryGetValue(workerId, out entry) || entry == null)
		{
			return false;
		}
		group.Aside.Remove(workerId);
		if (Time.time - entry.At > StepAsideSeconds)
		{
			return false;
		}
		from = entry.From;
		return true;
	}

	/// <summary>11/11/2026 (the body barrier): the piece left the queue for good (placed or given up) - its barrier and its
	/// short-retry count go with it.</summary>
	private static void ForgetBody(Group group, int index)
	{
		if (group == null)
		{
			return;
		}
		group.BodyAt.Remove(index);
		group.BodyTries.Remove(index);
	}

	/// <summary>
	/// P1 (review A7, 03/11/2026): the piece of this index is <b>done</b> for the site - counted once, out of the retry
	/// queue and its claim freed. False when it was already marked (a piece another worker built, or the same one counted
	/// twice), which is what keeps the numbers honest (<c>Placed</c> and <c>Skipped</c> are sets of indices and a piece
	/// that was given up and then went up counts as placed, never as both).
	/// </summary>
	internal static bool MarkDone(string key, int index)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null)
		{
			return false;
		}
		if (!group.Done.Add(index))
		{
			return false;
		}
		group.SkippedIndex.Remove(index);
		group.Claims.Remove(index);
		group.ClaimAt.Remove(index);
		ForgetBody(group, index);
		group.LastProgressAt = Time.time;
		for (int i = group.Retry.Count - 1; i >= 0; i--)
		{
			RetryEntry entry = group.Retry[i];
			if (entry != null && entry.Index == index)
			{
				group.Retry.RemoveAt(i);
			}
		}
		return true;
	}

	/// <summary>
	/// P1 (review A1, 03/11/2026): queues the piece for another attempt. False when the index is out of <b>real</b>
	/// attempts - the caller counts it as <b>skipped</b> (the piece is given up for the site, not silently lost).
	///
	/// <para>P1 (04/11/2026, the support gate): a failure by <b>dependency</b> (the piece below or the workbench is not up
	/// yet) is not the piece's fault - it does <b>not</b> spend an attempt, it comes back with a growing wait and the site
	/// keeps it until it stands or the stall watchdog gives up on the order. Without this the ridge pieces burned their 3
	/// tries before the support existed and were left out of the house.</para>
	///
	/// <para>11/11/2026 (the storey barrier): a piece refused because a <b>body</b> was on the spot
	/// (<see cref="BuildFailure.Kind.Blocked"/>) spends no attempt either - the pose is fine and the player is walking - and
	/// comes back after the short <c>BuildFailure.BodyRetryDelay</c> instead of the growing dependency wait. The plan does
	/// not climb over it: <see cref="NextRetryBelow"/> hands it back before the next storey.</para>
	/// </summary>
	internal static bool TryQueueRetry(string key, int index, BuildFailure.Kind kind)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null)
		{
			return false;
		}
		RetryEntry entry = null;
		for (int i = 0; i < group.Retry.Count; i++)
		{
			if (group.Retry[i] != null && group.Retry[i].Index == index)
			{
				entry = group.Retry[i];
				break;
			}
		}
		if (entry == null)
		{
			entry = new RetryEntry { Index = index };
			group.Retry.Add(entry);
		}
		if (kind == BuildFailure.Kind.Dependency)
		{
			entry.Dependency++;
			entry.At = Time.time + MHR_Build.DependencyDelay(entry.Dependency);
			return true;   // never spends an attempt, never counts as skipped
		}
		if (kind == BuildFailure.Kind.Blocked)
		{
			entry.Blocked++;
			entry.At = Time.time + BuildFailure.BodyRetryDelay;
			return true;   // a body on the spot: never spends an attempt either
		}
		if (entry.Attempts >= MHR_Build.MaxPasses)
		{
			// P1: out of attempts - the piece leaves the queue for good (the caller counts it as skipped). Leaving it
			// there would make the workers wait for a piece nobody may take.
			group.Retry.Remove(entry);
			return false;
		}
		entry.Attempts++;
		entry.At = Time.time + RetryDelay;
		return true;
	}

	/// <summary>P1: does the site still have pieces waiting for another attempt?</summary>
	internal static bool RetryPending(string key)
	{
		Group group;
		return !string.IsNullOrEmpty(key) && s_groups.TryGetValue(key, out group) && group != null && group.Retry.Count > 0;
	}

	/// <summary>
	/// P1 (review A1): the next piece of the retry queue this worker may take - it is <b>claimed here</b>, so two workers
	/// never pull the same index. -1 when there is nothing for it right now (all claimed by others, or not due yet).
	/// A built piece leaves the queue through <see cref="MarkDone"/>; an index that is done anyway (a stale entry) is
	/// dropped here (review B1, 03/11/2026).
	/// </summary>
	internal static int NextRetry(string key, int workerId)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null || group.Plan == null)
		{
			return -1;
		}
		for (int i = group.Retry.Count - 1; i >= 0; i--)
		{
			RetryEntry stale = group.Retry[i];
			if (stale == null || group.Done.Contains(stale.Index))
			{
				group.Retry.RemoveAt(i);
			}
		}
		float now = Time.time;
		for (int i = 0; i < group.Retry.Count; i++)
		{
			RetryEntry entry = group.Retry[i];
			if (entry == null || entry.Index < 0 || entry.Index >= group.Plan.Count || now < entry.At)
			{
				continue;
			}
			// 11/11/2026 (the body barrier): a piece of a storey ABOVE the one a body is holding is not pulled while the
			// barrier lives - the entrance that did not go up comes back BEFORE the work goes higher (the player's rule).
			if (!MayTake(group, entry.Index))
			{
				continue;
			}
			if (!TryClaim(group, entry.Index, workerId))
			{
				continue;   // another worker is on it
			}
			return entry.Index;
		}
		return -1;
	}

	/// <summary>
	/// 11/11/2026 (the <b>storey barrier</b> of the Run 9, the player's rule: "they may skip, but come back and try again
	/// before going up"): the plan index of a piece of the site's queue that is still <b>below</b> the piece the walk is about
	/// to take (-1 = none). It is the question <c>MHR_Build.StoreyBarrier</c> asks before climbing a storey: the entrance
	/// steps (or any piece a body or a missing support left in the queue) are settled first, and the row above waits for them.
	/// The index is returned (and not a yes/no) so the log can name the piece that did not come back.
	///
	/// <para>A candidate is a piece <b>before</b> <paramref name="nextIndex"/> in the plan and <b>below</b>
	/// <paramref name="nextY"/> (0,01 m of slack), not done, not given up, <b>not a door</b> (class 4 - doors stay last on
	/// purpose) and not already served by this barrier (<paramref name="served"/>). It does not look at the clock nor at the
	/// claims: a piece another worker is building below is still a piece the plan must not climb over.</para>
	/// </summary>
	internal static int PendingBelow(string key, int nextIndex, float nextY, HashSet<int> served)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null || group.Plan == null)
		{
			return -1;
		}
		int best = -1;
		float bestY = float.MaxValue;
		for (int i = 0; i < group.Retry.Count; i++)
		{
			RetryEntry entry = group.Retry[i];
			if (!IsBelow(group, entry, nextIndex, nextY, served))
			{
				continue;
			}
			float y = group.Plan[entry.Index].Pos.y;
			if (best < 0 || y < bestY)
			{
				best = entry.Index;
				bestY = y;
			}
		}
		return best;
	}

	/// <summary>
	/// 11/11/2026 (the storey barrier, Run 10): when the <b>earliest</b> piece of the site's queue that is still
	/// <b>below</b> <paramref name="nextY"/> becomes due (<see cref="RetryEntry.At"/>) - the patience of the barrier must
	/// not run before that (a piece deferred for a dependency comes back in up to 25 s, and the barrier's own 25 s were
	/// spent before it had its turn). <see cref="float.MaxValue"/> when nothing below is waiting. It is the same window as
	/// <see cref="PendingBelow"/> - <see cref="IsBelow"/> is the one definition.
	/// </summary>
	internal static float NextBelowAt(string key, int nextIndex, float nextY, HashSet<int> served)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null || group.Plan == null)
		{
			return float.MaxValue;
		}
		float due = float.MaxValue;
		for (int i = 0; i < group.Retry.Count; i++)
		{
			RetryEntry entry = group.Retry[i];
			if (!IsBelow(group, entry, nextIndex, nextY, served))
			{
				continue;
			}
			if (entry.At < due)
			{
				due = entry.At;
			}
		}
		return due;
	}

	/// <summary>
	/// 11/11/2026 (the storey barrier): the piece of the queue the barrier hands back <b>now</b> - the lowest one that is
	/// <b>due</b> (<c>At</c>), not claimed by another worker and not held by a body barrier of the site. It is <b>claimed
	/// here</b>, exactly like <see cref="NextRetry"/>, so two workers never pull the same index. -1 when nothing below is
	/// ready (the caller waits) or there is nothing below at all (the caller climbs).
	///
	/// <para>The <b>lowest</b> one wins: a column of refused pieces closes from the bottom up, and the bottom piece is the
	/// one the others lean on.</para>
	/// </summary>
	internal static int NextRetryBelow(string key, int nextIndex, float nextY, HashSet<int> served, int workerId)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null || group.Plan == null)
		{
			return -1;
		}
		float now = Time.time;
		int best = -1;
		float bestY = float.MaxValue;
		for (int i = 0; i < group.Retry.Count; i++)
		{
			RetryEntry entry = group.Retry[i];
			if (!IsBelow(group, entry, nextIndex, nextY, served) || now < entry.At)
			{
				continue;
			}
			int holder = OwnerOf(key, entry.Index);
			if (holder != 0 && holder != workerId)
			{
				continue;   // another worker is on it
			}
			if (!MayTake(group, entry.Index))
			{
				continue;   // a body barrier of the site holds it
			}
			float y = group.Plan[entry.Index].Pos.y;
			if (best < 0 || y < bestY)
			{
				best = entry.Index;
				bestY = y;
			}
		}
		if (best < 0 || !TryClaim(group, best, workerId))
		{
			return -1;
		}
		return best;
	}

	/// <summary>11/11/2026 (the storey barrier): is this queue entry a piece <b>below</b> the next one that the barrier may
	/// serve? The one definition of the window, shared by <see cref="PendingBelow"/> and <see cref="NextRetryBelow"/>.</summary>
	private static bool IsBelow(Group group, RetryEntry entry, int nextIndex, float nextY, HashSet<int> served)
	{
		if (entry == null || group.Plan == null)
		{
			return false;
		}
		int index = entry.Index;
		if (index < 0 || index >= nextIndex || index >= group.Plan.Count)
		{
			return false;   // not behind the walk, or not a piece of this plan
		}
		if (served != null && served.Contains(index))
		{
			return false;   // this barrier already gave it one turn
		}
		if (group.Done.Contains(index) || group.SkippedIndex.Contains(index))
		{
			return false;   // up, or out of the site for good
		}
		MHR_Build.PlannedStep step = group.Plan[index];
		if (step == null || step.Pos.y >= nextY - 0.01f)
		{
			return false;   // same storey or above: not what the barrier settles
		}
		return BuildBlueprintParser.ClassOf(step.Prefab) != 4;   // a door is always the last piece
	}

	/// <summary>
	/// P1 (review B2, 03/11/2026; the watchdog, 04/11/2026): the queue is given up - every entry is dropped and counted as
	/// skipped <b>once per index</b>, so a piece is never silently lost when the order ends. The caller receives the
	/// indices it just gave up (to name them in the log). A piece a worker is still building keeps its claim: if it does go
	/// up, <see cref="MarkDone"/> takes it out of the skipped set - a piece is never counted twice.
	/// </summary>
	internal static int AbandonRetry(string key, int workerId, List<int> pending)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null || group.Retry.Count == 0)
		{
			return 0;
		}
		int dropped = 0;
		for (int i = group.Retry.Count - 1; i >= 0; i--)
		{
			RetryEntry entry = group.Retry[i];
			group.Retry.RemoveAt(i);
			if (entry == null)
			{
				continue;
			}
			if (entry.Index < 0)
			{
				continue;
			}
			ForgetBody(group, entry.Index);
			if (pending != null)
			{
				pending.Add(entry.Index);
			}
			if (!group.Done.Contains(entry.Index) && group.SkippedIndex.Add(entry.Index))
			{
				dropped++;
			}
			int holder;
			if (workerId != 0 && group.Claims.TryGetValue(entry.Index, out holder) && holder == workerId)
			{
				group.Claims.Remove(entry.Index);
				group.ClaimAt.Remove(entry.Index);
			}
		}
		return dropped;
	}

	/// <summary>P1: how many companions are on the site (the plan is reordered only while it is a single one).</summary>
	private static int Workers(string key)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null)
		{
			return 0;
		}
		return group.Workers.Count;
	}

	/// <summary>P1: more than one worker walks the plan by index, so nobody reorders it (see <c>MHR_Build.PlanLocked</c>).</summary>
	internal static bool IsShared(string key)
	{
		return Workers(key) > 1;
	}

	/// <summary>P1: a piece of the plan was given up (the retry queue ran out of real attempts, or the watchdog dropped
	/// it). Counted <b>once per plan index</b> - and never for a piece that is already up.</summary>
	internal static bool AddSkipped(string key, int index)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null)
		{
			return false;
		}
		if (group.Done.Contains(index))
		{
			return false;
		}
		ForgetBody(group, index);
		return group.SkippedIndex.Add(index);
	}

	/// <summary>P1 (04/11/2026): the pieces of the plan that are up (a SET of indices - each piece once).</summary>
	internal static int Placed(string key)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null)
		{
			return 0;
		}
		return group.Done.Count;
	}

	/// <summary>P1 (04/11/2026): the pieces of the plan that were given up (a SET of indices - a piece that failed four
	/// times is one skipped piece, not four; that was the "262 puladas with 984 pieces" of the run of 03/11).</summary>
	internal static int Skipped(string key)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null)
		{
			return 0;
		}
		return group.SkippedIndex.Count;
	}

	/// <summary>P1 (04/11/2026): when a piece of this site last went up - the stall watchdog counts from here.</summary>
	internal static float LastProgressAt(string key)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null)
		{
			return 0f;
		}
		return group.LastProgressAt;
	}

	/// <summary>P1: the label of the site (the blueprint's name) - for the log lines of the members.</summary>
	internal static string Label(string key)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null)
		{
			return null;
		}
		return group.Label;
	}

	private static void Log(string message)
	{
		if (!CompanionsPlugin.LogNormal)
		{
			return;
		}
		CompanionsPlugin.Log?.LogInfo("[Build] " + message);
	}

	/// <summary>P1 (review B3, 03/11/2026): the details of the group that are not news at the normal end of an order.</summary>
	private static void LogDebug(string message)
	{
		if (!CompanionsPlugin.LogDetailed)
		{
			return;
		}
		CompanionsPlugin.Log?.LogDebug("[Build] " + message);
	}
}
