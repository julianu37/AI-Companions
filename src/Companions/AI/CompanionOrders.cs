using UnityEngine;

namespace Companions;

/// <summary>
/// Fase 3.1: turns a parsed order (LlmIntent) into what the mod already does. Everything here is a thin wrapper
/// around the same calls the radial menu and the command key use, so a chat order and a wheel order behave
/// identically - nothing new drives the companions, only a new way to ask.
/// </summary>
internal static class CompanionOrders
{
	/// <summary>What an order did, for the feedback line.</summary>
	internal sealed class OrderOutcome
	{
		/// <summary>The model's action was a real order (not "none").</summary>
		internal bool Understood;

		/// <summary>Localization key for the order's name ("hc_ai_order_wood"...).</summary>
		internal string LabelKey;

		/// <summary>How many were asked for (0 = the order didn't say).</summary>
		internal int Amount;

		/// <summary>How many companions actually took the order.</summary>
		internal int Count;

		/// <summary>Fase 3.2c: true when a companion already had what the order asked for in its own pack, so the
		/// order was never started (the work mode was left alone - see CompanionOrders.Apply).</summary>
		internal bool AlreadyHave;
	}

	internal static OrderOutcome Run(LlmIntent intent)
	{
		OrderOutcome outcome = new OrderOutcome();
		if (intent == null || intent.IsNone)
		{
			return outcome;
		}
		Player player = Player.m_localPlayer;
		if (player == null)
		{
			return outcome;
		}
		string action = intent.Action;
		outcome.Understood = true;
		outcome.LabelKey = "hc_ai_order_" + action;
		outcome.Amount = intent.Amount;

		if (action == "come")
		{
			// reuse the mod's own "call everyone here" order (holding the command key)
			int inScope = 0;
			foreach (CompanionSetup companion in CompanionSetup.AllCompanions)
			{
				if (companion != null && CompanionInteract.MHR_InScope(companion))
				{
					inScope++;
				}
			}
			if (inScope > 0)
			{
				CompanionInteract.MHR_FireSuperRecall(player);
				outcome.Count = inScope;
			}
			CompanionsPlugin.Log?.LogInfo($"[IA] Order \"come\" -> {outcome.Count} companion(s) recalled");
			return outcome;
		}

		foreach (CompanionSetup companion2 in CompanionSetup.AllCompanions)
		{
			if (companion2 != null && Apply(companion2, action, intent.Amount, intent.Resource, outcome))
			{
				outcome.Count++;
			}
		}
		CompanionsPlugin.Log?.LogInfo($"[IA] Order \"{action}\"{(outcome.Amount > 0 ? $" (x{outcome.Amount})" : "")} -> {outcome.Count} companion(s)"
			+ (outcome.AlreadyHave ? " (and at least one already had enough)" : ""));
		if (outcome.Count == 0 && !outcome.AlreadyHave)
		{
			CompanionsPlugin.Log?.LogInfo("[IA] No companion answered - none of yours is in range, or their Command switch is off (wheel)");
		}
		return outcome;
	}

	/// <summary>True when this companion took the order. Null-safe, main thread only.</summary>
	private static bool Apply(CompanionSetup companion, string action, int amount, string resource, OrderOutcome outcome)
	{
		if (companion == null || companion.GetComponent<ZNetView>()?.GetZDO() == null)
		{
			return false;
		}
		// Command is a switch on the wheel: it has to work even for a companion whose Command is currently off,
		// so it is the one order that skips the scope check.
		if (action == "command" || action == "nocommand")
		{
			if (!MHR_Owner.IsMine(companion))
			{
				return false;
			}
			MHR_Owner.TakeControl(companion);
			companion.SetIsCommandable(action == "command");
			return true;
		}
		// everything else is a player order: own companion, in range, Command on (MHR_InScope also takes control)
		if (!CompanionInteract.MHR_InScope(companion))
		{
			return false;
		}
		MHR_Owner.TakeControl(companion);

		switch (action)
		{
		case "follow":
			CompanionSetup.ClearOrderCap(companion);
			SetFollow(companion);
			return true;
		case "stop":
			CompanionSetup.ClearOrderCap(companion);
			SetFollow(companion);
			ClearCombatTarget(companion);
			return true;
		case "sethome":
			companion.SetHomePosition(companion.transform.position);
			return true;
		case "wander":
			companion.SetWander(true);
			if (!companion.HasHomePosition())
			{
				companion.SetHomePosition(companion.transform.position);
			}
			return true;
		case "nowander":
			companion.SetWander(false);
			return true;
		case "pickup":
			SetAutoPickup(companion, true);
			return true;
		case "nopickup":
			SetAutoPickup(companion, false);
			return true;
		case "stance_balanced":
			companion.SetCombatStance(CompanionSetup.StanceBalanced);
			return true;
		case "stance_passive":
			companion.SetCombatStance(CompanionSetup.StancePassive);
			return true;
		case "stance_ranged":
			companion.SetCombatStance(CompanionSetup.StanceRanged);
			return true;
		case "stance_melee":
			companion.SetCombatStance(CompanionSetup.StanceMelee);
			return true;
		default:
		{
			int mode = JobMode(action);
			if (mode < 0)
			{
				return false;
			}
			// Fase 3.2c: the number is a TOTAL counted in the companion's own pack, and it is about the item the
			// order named ("colete 10 madeiras" = have 10 wood on you - see MHR_GatherRules.Bag). If it already has
			// that much, the order is not started at all and it says so: nothing is touched, so a companion chopping
			// wood keeps chopping and is not sent off to gather stone it doesn't need. Resolved before the target of
			// the order that is running is dropped, so a refused order doesn't disturb that one either.
			string item = (amount > 0 && HasOrderCap(mode)) ? OrderItems.Resolve(action, resource, mode) : null;
			if (item != null && MHR_GatherRules.Bag(companion, item) >= amount)
			{
				outcome.AlreadyHave = true;
				CompanionsPlugin.Log?.LogInfo($"[IA] \"{action}\" x{amount}: {NameOf(companion)} already has {amount}+ {item} in its pack - the order wasn't started");
				CompanionTalk talk = companion.GetComponent<CompanionTalk>();
				if (talk != null)
				{
					talk.Say(ModLocalization.Loc("hc_ai_already_have"), true);
				}
				return false;
			}
			// the wheel toggles a job off when it's picked twice; an order always sets it
			CompanionSetup.ClearOrderCap(companion);   // a new job order drops the target of the old one
			CompanionSetup.ApplyActionMode(companion, mode, toggle: false);
			// Fase 3.2: "colete 5 madeiras" - the number caps the job, so it stops well before the config's 500.
			// Fase 3.2c: the number is a total counted in the pack, for the item the order named; when the order
			// named no item the game knows, it caps the whole job (as in Fase 3.2).
			if (amount > 0 && HasOrderCap(mode))
			{
				CompanionSetup.SetOrderCap(companion, mode, amount, item);
				CompanionsPlugin.Log?.LogInfo($"[IA] Order target: mode {mode} stops at {amount} {(item ?? "of every item on the job's list")} in the pack");
			}
			return true;
		}
		}
	}

	/// <summary>Fase 3.2: jobs that stop at an amount, so a number in the order can be their target (wood, stone,
	/// ore - the same three MHR_GatherRules keeps a limit for).</summary>
	internal static bool HasOrderCap(int mode)
	{
		return mode == CompanionSetup.ModeGatherWood || mode == CompanionSetup.ModeGatherStone || mode == CompanionSetup.ModeGatherOre;
	}

	/// <summary>The work mode behind an order, or -1 when the action isn't a job.</summary>
	internal static int JobMode(string action)
	{
		switch (action)
		{
		case "wood":
			return CompanionSetup.ModeGatherWood;
		case "stone":
			return CompanionSetup.ModeGatherStone;
		case "ore":
			return CompanionSetup.ModeGatherOre;
		case "forage":
			return CompanionSetup.ModeForage;
		case "smelt":
			return CompanionSetup.ModeSmelt;
		case "hunt":
			return CompanionSetup.ModeHunt;
		case "farm":
			return CompanionSetup.ModeFarm;
		case "fish":
			return CompanionSetup.ModeFish;
		case "repair":
			return CompanionSetup.ModeRepairBuildings;
		case "cook":
			return CompanionSetup.ModeCook;
		default:
			return -1;
		}
	}

	// "follow" and "stop" both mean "come back and follow me": a job and Follow can't both be on, and the job that
	// had paused Follow must not turn it back on later (same as the wheel does it).
	private static void SetFollow(CompanionSetup companion)
	{
		MHR_JobRules.FollowSetByPlayer(companion);
		companion.SetFollow(true);
		CompanionSetup.ApplyActionMode(companion, CompanionSetup.ModeFollow, toggle: false);
	}

	private static void ClearCombatTarget(CompanionSetup companion)
	{
		companion.GetComponent<CompanionAI>()?.ClearTargets();
		CompanionCombatAI combat = companion.GetComponent<CompanionCombatAI>();
		if (combat != null)
		{
			combat.Disengage();
		}
	}

	private static void SetAutoPickup(CompanionSetup companion, bool value)
	{
		companion.GetComponent<ZNetView>()?.GetZDO()?.Set(CompanionSetup.AutoPickupHash, value);
	}

	/// <summary>The companion's name, for the log.</summary>
	private static string NameOf(CompanionSetup companion)
	{
		ZDO zdo = companion != null ? companion.GetComponent<ZNetView>()?.GetZDO() : null;
		return zdo != null ? zdo.GetString(CompanionSetup.NameHash, "Companion") : "Companion";
	}
}
