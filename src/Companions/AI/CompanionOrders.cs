using System.Collections.Generic;
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

		/// <summary>Fase 3.6: the finished line to show (already localized), for the orders whose feedback has to
		/// name the creature or the item ("Atacando o troll.", "Não tenho picareta."). When set, it wins over the
		/// generic "Ok: ..." line built by CompanionChatAI.</summary>
		internal string Message;
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

		// Fase 3.6: the orders that point at one creature or one item in the companion's pack
		if (action == "attack")
		{
			return Attack(player, intent, outcome);
		}
		if (action == "equip")
		{
			return Equip(intent, outcome);
		}
		if (action == "drop")
		{
			return Drop(intent, outcome);
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

	// ---- Fase 3.6: orders that point at one creature or one item ----

	/// <summary>"ataca o greydwarf": the companion engages that one creature. The creature is looked for under the
	/// player's crosshair first (point at it and say "ataca aquele"), then among the creatures within
	/// [IA] RaioDeAlvo of the player by name. When nothing matches, the player is told - nothing is engaged.
	/// The order goes through CompanionAI.DirectEngage, the very path the radial menu's attack uses, so hunting
	/// gear, sneaking and looting of harmless game stay exactly the same.</summary>
	private static OrderOutcome Attack(Player player, LlmIntent intent, OrderOutcome outcome)
	{
		float range = ModConfig.AiTargetRange != null ? ModConfig.AiTargetRange.Value : 40f;
		Character creature = OrderTargets.FindCreature(intent.Target, player, range);
		if (creature == null)
		{
			outcome.Message = string.IsNullOrEmpty(intent.Target)
				? ModLocalization.Loc("hc_ai_no_aim")
				: ModLocalization.LocFmt("hc_ai_no_target", Spoken(intent.Target));
			CompanionsPlugin.Log?.LogInfo($"[IA] Order \"attack\" \"{intent.Target}\": no creature matched within {range:F0} m");
			return outcome;
		}
		string name = OrderTargets.NameOf(creature);
		CompanionTalk talk = null;
		foreach (CompanionSetup companion in CompanionSetup.AllCompanions)
		{
			if (companion == null || !CompanionInteract.MHR_InScope(companion))
			{
				continue;
			}
			CompanionAI ai = companion.GetComponent<CompanionAI>();
			if (ai == null)
			{
				continue;
			}
			// same as the radial menu's attack: an order to attack drops whatever the companion was on its way to do
			CompanionInteract.CancelExistingActions(companion);
			// Fase 3.6: an explicit order is carried out even in Passive stance - the order covers this one creature
			// and the stance itself is left alone (CompanionCombatAI.EngageByOrder). Without this the order would be
			// swallowed silently, because Passive is exactly "never starts a fight" (see package/README).
			if (companion.GetCombatStance() == CompanionSetup.StancePassive)
			{
				CompanionsPlugin.Log?.LogInfo($"[IA] {NameOf(companion)} is on the Passive stance - attacking \"{name}\" anyway, for this order only");
			}
			ai.DirectEngage(creature, byOrder: true);
			if (talk == null)
			{
				talk = companion.GetComponent<CompanionTalk>();
			}
			outcome.Count++;
		}
		if (outcome.Count == 0)
		{
			outcome.Message = ModLocalization.Loc("hc_ai_no_companion");
			CompanionsPlugin.Log?.LogInfo("[IA] Order \"attack\": no companion took it (out of range or Command off)");
			return outcome;
		}
		outcome.Message = ModLocalization.LocFmt("hc_ai_attacking", name);
		if (talk != null)
		{
			talk.Say(outcome.Message, true);
		}
		CompanionsPlugin.Log?.LogInfo($"[IA] Order \"attack\" -> \"{name}\" ({outcome.Count} companion(s))");
		return outcome;
	}

	/// <summary>"equipa o machado de bronze": the item is looked for in the companion's OWN pack (by prefab name,
	/// by the in-game name in either language) and equipped with Humanoid.EquipItem - the same call the mod's own
	/// gear handling uses. Note that auto-equip may later swap it back for the best weapon for the job at hand;
	/// the order does not change any setting, it only puts the item in its hand now.</summary>
	private static OrderOutcome Equip(LlmIntent intent, OrderOutcome outcome)
	{
		if (string.IsNullOrEmpty(intent.Target))
		{
			outcome.Message = ModLocalization.Loc("hc_ai_need_item");
			return outcome;
		}
		string found = null;
		bool notEquipable = false;
		CompanionTalk talk = null;
		foreach (CompanionSetup companion in CompanionSetup.AllCompanions)
		{
			if (companion == null || !CompanionInteract.MHR_InScope(companion))
			{
				continue;
			}
			Humanoid humanoid = companion.GetComponent<Humanoid>();
			if (humanoid == null)
			{
				continue;
			}
			ItemDrop.ItemData item = OrderTargets.FindItem(humanoid, intent.Target, out int score);
			if (item == null)
			{
				continue;
			}
			if (found == null)
			{
				found = OrderTargets.NameOf(item);
			}
			if (!item.IsEquipable())
			{
				notEquipable = true;
				CompanionsPlugin.Log?.LogInfo($"[IA] Order \"equip\": {NameOf(companion)} has \"{found}\" but it can't be equipped");
				continue;
			}
			if (humanoid.IsItemEquiped(item) || humanoid.EquipItem(item))
			{
				outcome.Count++;
				if (talk == null)
				{
					talk = companion.GetComponent<CompanionTalk>();
				}
				CompanionsPlugin.Log?.LogInfo($"[IA] Order \"equip\": {NameOf(companion)} equipped \"{found}\" (match {score})");
			}
		}
		if (outcome.Count > 0)
		{
			outcome.Message = ModLocalization.LocFmt("hc_ai_equipped", found);
			if (talk != null)
			{
				talk.Say(outcome.Message, true);
			}
			return outcome;
		}
		outcome.Message = notEquipable
			? ModLocalization.LocFmt("hc_ai_not_equipable", found)
			: ModLocalization.LocFmt("hc_ai_no_item", Spoken(intent.Target));
		CompanionsPlugin.Log?.LogInfo($"[IA] Order \"equip\" \"{intent.Target}\": {(notEquipable ? "the item can't be equipped" : "none of the companions has it")}");
		return outcome;
	}

	/// <summary>"larga a pedra" / "joga fora 5 madeira": the item is dropped out of the companion's own pack, in
	/// front of it. Without a number the WHOLE stack goes (the order is "get rid of it", and that is also what the
	/// mod does with junk it clears out - see MHR_GatherRules.TryMakeRoom); with a number, at most that many. The
	/// pile is taken out of the auto-pickup layer so the companion doesn't pick its own order back up.</summary>
	private static OrderOutcome Drop(LlmIntent intent, OrderOutcome outcome)
	{
		if (string.IsNullOrEmpty(intent.Target))
		{
			outcome.Message = ModLocalization.Loc("hc_ai_need_item");
			return outcome;
		}
		string found = null;
		int total = 0;
		CompanionTalk talk = null;
		foreach (CompanionSetup companion in CompanionSetup.AllCompanions)
		{
			if (companion == null || !CompanionInteract.MHR_InScope(companion))
			{
				continue;
			}
			Humanoid humanoid = companion.GetComponent<Humanoid>();
			Inventory inventory = humanoid != null ? humanoid.GetInventory() : null;
			if (inventory == null)
			{
				continue;
			}
			int dropped = DropNamed(humanoid, inventory, intent.Target, intent.Amount, out string itemName);
			if (dropped <= 0)
			{
				continue;
			}
			if (found == null)
			{
				found = itemName;
			}
			total += dropped;
			outcome.Count++;
			if (talk == null)
			{
				talk = companion.GetComponent<CompanionTalk>();
			}
			CompanionsPlugin.Log?.LogInfo($"[IA] Order \"drop\": {NameOf(companion)} dropped {dropped} x \"{itemName}\"");
		}
		if (total > 0)
		{
			outcome.Message = ModLocalization.LocFmt("hc_ai_dropped", found, total);
			if (talk != null)
			{
				talk.Say(outcome.Message, true);
			}
			return outcome;
		}
		outcome.Message = ModLocalization.LocFmt("hc_ai_no_item", Spoken(intent.Target));
		CompanionsPlugin.Log?.LogInfo($"[IA] Order \"drop\" \"{intent.Target}\": none of the companions has it");
		return outcome;
	}

	/// <summary>Drops what the order named out of one companion's pack and returns how many units went out.
	/// <paramref name="amount"/> 0 = every stack of it. Walks a copy of the item list, because dropping changes
	/// the inventory's own list.</summary>
	private static int DropNamed(Humanoid humanoid, Inventory inventory, string target, int amount, out string itemName)
	{
		itemName = null;
		int dropped = 0;
		int left = amount;
		List<ItemDrop.ItemData> items = new List<ItemDrop.ItemData>(inventory.GetAllItems());
		for (int i = 0; i < items.Count; i++)
		{
			ItemDrop.ItemData item = items[i];
			if (item == null || item.m_shared == null || OrderTargets.ScoreItem(target, item) < OrderTargets.MinScore)
			{
				continue;
			}
			// the game refuses to drop quest items (Humanoid.DropItem says "$msg_cantdrop"); this path drops the
			// pile itself, so the same rule is applied here
			if (item.m_shared.m_questItem)
			{
				itemName = OrderTargets.NameOf(item);
				CompanionsPlugin.Log?.LogInfo($"[IA] Order \"drop\": \"{itemName}\" is a quest item - not dropping it");
				continue;
			}
			int take = amount > 0 ? Mathf.Min(left, item.m_stack) : item.m_stack;
			if (take <= 0)
			{
				continue;
			}
			if (itemName == null)
			{
				itemName = OrderTargets.NameOf(item);
			}
			if (humanoid.IsItemEquiped(item))
			{
				humanoid.UnequipItem(item, triggerEquipEffects: false);
			}
			Vector3 at = humanoid.transform.position + humanoid.transform.forward * 0.8f + Vector3.up * 0.5f;
			ItemDrop drop = ItemDrop.DropItem(item, take, at, Quaternion.identity);
			if (drop != null)
			{
				// the companion's own auto-pickup would otherwise grab it straight back
				drop.m_autoPickup = false;
			}
			inventory.RemoveItem(item, take);
			dropped += take;
			left -= take;
			if (amount > 0 && left <= 0)
			{
				break;
			}
		}
		return dropped;
	}

	/// <summary>The words the player used, back in a readable shape (the order's target is kept normalized:
	/// "machado_de_bronze").</summary>
	private static string Spoken(string target)
	{
		return string.IsNullOrEmpty(target) ? "?" : target.Replace('_', ' ');
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
