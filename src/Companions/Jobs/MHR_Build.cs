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

	/// <summary>How close to the spot it stops and places.</summary>
	internal const float Reach = 2.5f;

	/// <summary>E4b: how long a pending build waits for the gather before it is dropped.</summary>
	internal const float GatherTimeout = 300f;

	private const string LogTag = "[Build] ";

	private enum Phase
	{
		Find,
		Goto,
		Place,
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
		internal Vector3 Hint;
		internal int Mode;
		internal string Item;
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
		else if (Time.time - state.Start > Timeout)
		{
			why = "the order took too long";
		}
		if (why != null)
		{
			Finish(ai, state, "hc_ai_build_failed", why);
			return false;
		}

		switch (state.Phase)
		{
			case Phase.Find:
				return Find(ai, state);
			case Phase.Goto:
				return Goto(ai, state, dt);
			case Phase.Place:
				return Place(ai, state, cs, bag);
			case Phase.Verify:
				return Verify(ai, state, cs);
			default:
				Finish(ai, state, null, "done");
				return false;
		}
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
		BuildSiteResult site = BuildSite.Find(state.Piece, state.Hint, state.Forward, radius, 3, 8);
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
		if (flat <= Reach && Mathf.Abs(me.y - state.Pos.y) <= 3f)
		{
			ai.StopMoving();
			MHR_Mover.Release(ai);
			state.Phase = Phase.Place;
			return true;
		}
		MHR_Mover.Go(ai, dt, state.Pos, Reach, true, "Build");
		if (MHR_Mover.GaveUp(ai))
		{
			Log(ai, "can't get to the spot");
			Finish(ai, state, "hc_ai_build_no_site", null, LabelOf(state));
			return false;
		}
		return true;
	}

	private static bool Place(CompanionAI ai, State state, CompanionSetup cs, Inventory bag)
	{
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
			bool gather = ModConfig.BuildGatherForBuild == null || ModConfig.BuildGatherForBuild.Value;
			if (gather && StartGather(ai, state, cs, missing))
			{
				Finish(ai, state, "hc_ai_build_collecting", "gathering first", MissingText(missing), LabelOf(state));
				return false;
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
		Log(ai, "done: " + state.Prefab + " at (" + F(state.Pos.x) + ", " + F(state.Pos.y) + ", " + F(state.Pos.z) + ")");
		Finish(ai, state, "hc_ai_build_done", null, LabelOf(state));
		return false;
	}

	/// <summary>Gives the piece's cost back to the bag when the placement itself failed (the consume already
	/// happened). Never called after a success.</summary>
	/// <summary>
	/// E4b: turns the missing materials into the NORMAL gather order (mode + order target = exactly the shortfall)
	/// and remembers a pending build for the watcher. The build order ENDS here - the harvest owns the movement.
	/// </summary>
	private static bool StartGather(CompanionAI ai, State state, CompanionSetup cs, Dictionary<string, int> missing)
	{
		string item = null;
		int mode = 0;
		int amount = 0;
		foreach (KeyValuePair<string, int> kv in missing)
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
			return false;
		}
		CompanionSetup.ApplyActionMode(cs, mode, false);
		CompanionSetup.SetOrderCap(cs, mode, amount, item);
		s_pending[ai.GetInstanceID()] = new Pending
		{
			Prefab = state.Prefab,
			Hint = state.Hint,
			Mode = mode,
			Item = item,
			Start = Time.time
		};
		Log(ai, "gathering " + amount + "x " + item + " first (mode " + mode + ") - the build resumes after it");
		return true;
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
		Piece piece = BuildProbe.PieceOf(pending.Prefab);
		if (piece == null)
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
		if (Time.time - pending.Start > GatherTimeout)
		{
			s_pending.Remove(ai.GetInstanceID());
			CompanionSetup.ClearOrderCap(cs);
			Log(ai, "gathering took too long - the build order was dropped");
			return;
		}
		bool useChests = ModConfig.BuildUseChests == null || ModConfig.BuildUseChests.Value;
		if (BuildStock.Missing(bag, piece, pending.Hint, cs, useChests).Count > 0)
		{
			return;   // still gathering
		}
		s_pending.Remove(ai.GetInstanceID());
		CompanionSetup.ClearOrderCap(cs);
		// back to Follow mode BEFORE the build starts: the harvest must not keep steering while the build does
		CompanionSetup.ApplyActionMode(cs, CompanionSetup.ModeFollow, false);
		if (Start(cs, pending.Prefab, pending.Hint))
		{
			Log(ai, "materials ready - resuming the build");
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
