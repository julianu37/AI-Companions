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

	/// <summary>P1 (review A1): one piece of the plan that did not stand yet. It stays in the queue until the piece is
	/// done or runs out of attempts (<c>MHR_Build.MaxPasses</c>).</summary>
	private sealed class RetryEntry
	{
		internal int Index;
		/// <summary>How many times it was queued (the cap is per index, shared by the whole site).</summary>
		internal int Attempts;
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
		/// <summary>P1 (review A1): the pieces that did not stand yet - the site's queue, never a worker's.</summary>
		internal readonly List<RetryEntry> Retry = new List<RetryEntry>();
		internal int Placed;
		internal int Skipped;
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
			LeaderId = leaderId
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
	/// instead of placing a second one on top of it.
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
			if (entry != null && entry.Index == index && entry.Attempts > 0)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// P1 (review A7, 03/11/2026): the piece of this index is <b>done</b> for the site - counted once, out of the retry
	/// queue and its claim freed. False when it was already marked (a piece another worker built, or the same one counted
	/// twice), which is what keeps <c>Placed</c> honest.
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
		group.Placed++;
		group.Claims.Remove(index);
		group.ClaimAt.Remove(index);
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
	/// P1 (review A1, 03/11/2026): queues the piece for another attempt. False when the index is out of attempts - the
	/// caller counts it as <b>skipped</b> (the piece is given up for the site, not silently lost). The attempts are per
	/// plan index and shared by the workers, so the site tries a piece <c>MHR_Build.MaxPasses</c> times at most.
	/// </summary>
	internal static bool TryQueueRetry(string key, int index)
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
			if (!TryClaim(group, entry.Index, workerId))
			{
				continue;   // another worker is on it
			}
			return entry.Index;
		}
		return -1;
	}

	/// <summary>
	/// P1 (review B2, 03/11/2026): the worker gave up waiting for the site's retry queue - the entries it could have taken
	/// are <b>dropped and counted as skipped</b>, so a piece is never silently lost when the group ends with a full queue.
	/// An entry another live worker is on stays (that piece may still go up). Returns how many were dropped.
	/// </summary>
	internal static int AbandonRetry(string key, int workerId)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null || group.Retry.Count == 0)
		{
			return 0;
		}
		float now = Time.time;
		int dropped = 0;
		for (int i = group.Retry.Count - 1; i >= 0; i--)
		{
			RetryEntry entry = group.Retry[i];
			if (entry == null)
			{
				group.Retry.RemoveAt(i);
				continue;
			}
			int holder;
			float at;
			bool live = group.Claims.TryGetValue(entry.Index, out holder) && holder != workerId
				&& group.ClaimAt.TryGetValue(entry.Index, out at) && now - at < ClaimSeconds;
			if (live)
			{
				continue;
			}
			group.Retry.RemoveAt(i);
			group.Claims.Remove(entry.Index);
			group.ClaimAt.Remove(entry.Index);
			group.Skipped++;
			dropped++;
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

	/// <summary>P1: a piece of the plan was given up by a worker (the retry queue ran out of attempts for it).</summary>
	internal static void AddSkipped(string key)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null)
		{
			return;
		}
		group.Skipped++;
	}

	internal static int Placed(string key)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null)
		{
			return 0;
		}
		return group.Placed;
	}

	internal static int Skipped(string key)
	{
		Group group;
		if (string.IsNullOrEmpty(key) || !s_groups.TryGetValue(key, out group) || group == null)
		{
			return 0;
		}
		return group.Skipped;
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
