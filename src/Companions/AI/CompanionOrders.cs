using System;
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

	/// <summary>Runs one order. <paramref name="userText"/> is what the player actually said (typed in the chat or
	/// spoken): Fase 3.6b uses it as the safety net for attack/equip/drop when the model answered "target": null.
	/// <paramref name="speaker"/> (Fase 3.8) is the companion the message was addressed to, when the caller knows it -
	/// the inventory order opens the window of that one companion.</summary>
	internal static OrderOutcome Run(LlmIntent intent, string userText, CompanionSetup speaker = null)
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
		// Fase 3.1 (correção do teste em jogo): um modelo pequeno às vezes devolve "action" e "resource"
		// inconsistentes ("colete 10 pedras" voltou como action "wood" com resource "stone") e o mod obedecia ao
		// "action" genérico e ignorava o recurso - que é o campo específico, o que já foi resolvido para o item que
		// o trabalho conta. Quando os dois discordam sobre QUAL trabalho é, o recurso manda (só nas ordens de coleta;
		// ver OrderItems.GatherAction). Corrigido aqui, uma vez por mensagem, para o log sair uma linha só.
		string byResource = OrderItems.GatherAction(action, intent.Resource);
		if (byResource != null)
		{
			CompanionsPlugin.Log?.LogInfo($"[IA] O modelo devolveu action=\"{action}\" com resource=\"{intent.Resource}\" - usei \"{byResource}\" (o recurso manda).");
			action = byResource;
		}
		// Fase 3.1 (3ª correção do teste em jogo): "drop", "pickup" e "nopickup" são ações vizinhas e o modelo
		// pequeno troca as três ("jogue fora sua madeira" voltou como "nopickup", que DESLIGA a coleta automática
		// em vez de largar a madeira). Mesma ideia do "o recurso manda": quando o modelo devolve "nopickup" mas o
		// jogador pediu claramente para largar ou para recolher, as palavras do jogador mandam. Uma chamada por
		// mensagem; sem gatilho, tudo fica exatamente como estava. O alvo do "drop" continua vindo do caminho da
		// Fase 3.6b (as palavras do jogador, validadas contra a mochila) - nada de extração de nome nova.
		string byWords = PickupIntent.NeighborAction(action, userText);
		if (byWords != null)
		{
			CompanionsPlugin.Log?.LogInfo($"[IA] O modelo devolveu \"{action}\" mas o jogador pediu para {(byWords == PickupIntent.DropAction ? "largar" : "recolher")} - usei \"{byWords}\".");
			action = byWords;
		}
		outcome.Understood = true;
		outcome.LabelKey = "hc_ai_order_" + action;
		outcome.Amount = intent.Amount;

		// Fase 3.1: "recolhe os itens do chão" é uma ordem de UM TIRO (MHR_Collect), então qualquer outra ordem a
		// derruba - feito aqui, uma vez por mensagem, para que até as ordens que nunca chegam ao Apply
		// (attack/equip/drop/inventory) a parem. A roda faz o mesmo por CompanionInteract.CancelExistingActions.
		if (action != "pickup")
		{
			MHR_Collect.CancelAll();
		}

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

		// Fase 3.6: the orders that point at one creature or one item in the companion's pack. Fase 3.6b: the
		// player's own message goes with them, because the model does not always fill in "target" (see
		// ResolveItemTarget / Attack).
		if (action == "attack")
		{
			return Attack(player, intent, userText, outcome);
		}
		if (action == "equip")
		{
			return Equip(intent, userText, outcome);
		}
		if (action == "drop")
		{
			return Drop(intent, userText, outcome);
		}

		// Fase 3.8: the one order that is about ONE companion - the player sees its pack
		if (action == "inventory")
		{
			return Inventory(player, speaker, outcome);
		}
		if (action == "inventory_close")
		{
			return CloseInventory(outcome);
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
	/// gear, sneaking and looting of harmless game stay exactly the same.
	///
	/// Fase 3.6b: the model sometimes answers "target": null even though the player named the creature ("ataca o
	/// greydwarf" came back empty and nothing was engaged). The player's OWN words are then read for a creature
	/// name (<see cref="OrderTargets.FindCreatureByWords"/>) before the aim is used, so a spoken/typed order works
	/// even when the model drops the field. Only a real name match counts; "ataca aquele" names nothing and still
	/// goes to the crosshair.</summary>
	private static OrderOutcome Attack(Player player, LlmIntent intent, string message, OrderOutcome outcome)
	{
		float range = ModConfig.AiTargetRange != null ? ModConfig.AiTargetRange.Value : 40f;
		string target = intent.Target;
		Character creature = null;
		if (!string.IsNullOrEmpty(target))
		{
			creature = OrderTargets.FindCreature(target, player, range);
		}
		if (creature == null)
		{
			creature = OrderTargets.FindCreatureByWords(message, player, range, out string phrase);
			if (creature != null)
			{
				CompanionsPlugin.Log?.LogInfo($"[IA] Order \"attack\": the model named "
					+ (string.IsNullOrEmpty(target) ? "nothing" : $"\"{target}\", which isn't around")
					+ $" - the player's own words named \"{phrase}\" (Fase 3.6b fallback)");
				target = phrase;
			}
			else if (string.IsNullOrEmpty(target))
			{
				// nothing named anywhere: the creature under the crosshair, exactly as in Fase 3.6 ("ataca aquele")
				creature = OrderTargets.FindCreature(null, player, range);
			}
		}
		if (creature == null)
		{
			outcome.Message = string.IsNullOrEmpty(target)
				? ModLocalization.Loc("hc_ai_no_aim")
				: ModLocalization.LocFmt("hc_ai_no_target", Spoken(target));
			CompanionsPlugin.Log?.LogInfo($"[IA] Order \"attack\" \"{target}\": no creature matched within {range:F0} m");
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
	/// the order does not change any setting, it only puts the item in its hand now.
	///
	/// Fase 3.6b: when the model left "target" empty (or named something no companion carries), the player's own
	/// words are read for the item (see ResolveItemTarget).</summary>
	private static OrderOutcome Equip(LlmIntent intent, string message, OrderOutcome outcome)
	{
		string target = ResolveItemTarget(intent.Target, message, "equip");
		if (string.IsNullOrEmpty(target))
		{
			outcome.Message = ModLocalization.Loc("hc_ai_need_item");
			CompanionsPlugin.Log?.LogInfo("[IA] Order \"equip\": the model named no item and nothing in the player's message answers one the companions carry");
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
			ItemDrop.ItemData item = OrderTargets.FindItem(humanoid, target, out int score);
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
			: ModLocalization.LocFmt("hc_ai_no_item", Spoken(target));
		CompanionsPlugin.Log?.LogInfo($"[IA] Order \"equip\" \"{target}\": {(notEquipable ? "the item can't be equipped" : "none of the companions has it")}");
		return outcome;
	}

	/// <summary>"larga a pedra" / "joga fora 5 madeira": the item is dropped out of the companion's own pack, in
	/// front of it. Without a number the WHOLE stack goes (the order is "get rid of it", and that is also what the
	/// mod does with junk it clears out - see MHR_GatherRules.TryMakeRoom); with a number, at most that many. The
	/// pile is taken out of the auto-pickup layer so the companion doesn't pick its own order back up.
	///
	/// Fase 3.6b: this is the order the bug report was about ("Joga essas pedras que tu pegou no chão" and "Joga 10
	/// pedras no chão" both came back from the model with "target": null, so there was no item to drop and nothing
	/// happened). The player's own words are now read for the item, exactly as for equip.</summary>
	private static OrderOutcome Drop(LlmIntent intent, string message, OrderOutcome outcome)
	{
		string target = ResolveItemTarget(intent.Target, message, "drop");
		if (string.IsNullOrEmpty(target))
		{
			outcome.Message = ModLocalization.Loc("hc_ai_need_item");
			CompanionsPlugin.Log?.LogInfo("[IA] Order \"drop\": the model named no item and nothing in the player's message answers one the companions carry");
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
			int dropped = DropNamed(humanoid, inventory, target, intent.Amount, out string itemName);
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
		outcome.Message = ModLocalization.LocFmt("hc_ai_no_item", Spoken(target));
		CompanionsPlugin.Log?.LogInfo($"[IA] Order \"drop\" \"{target}\": none of the companions has it");
		return outcome;
	}

	// ---- Fase 3.8: the companion's own inventory, opened for the player ----

	/// <summary>"abre o teu inventário" / "mostra o que você carrega": the player gets the companion's own window,
	/// exactly as if he had walked up to it and pressed the interact key - the order goes through the very same
	/// CompanionInteract.OpenCompanionInventory the tap uses (InventoryGui.Show + the mod's companion panel), so the
	/// window, the name field, the food slots and the gear panel are the ones the mod already shows.
	///
	/// ONE companion is picked, never all of them: the one the message was addressed to
	/// (<paramref name="speaker"/>, chosen by CompanionChatAI.PickSpeaker when the player spoke) and, when that one
	/// is gone by the time the answer lands, the nearest one that could take the order (the same rule, read again).
	///
	/// When the container isn't ours yet (multiplayer: another client holds the companion), the ownership is asked
	/// for - the same call the interact key makes - and the window opens by itself as soon as it arrives
	/// (CompanionInteract.RequestCompanionInventory → TickPendingInventoryOpen): the player asked once.</summary>
	private static OrderOutcome Inventory(Player player, CompanionSetup speaker, OrderOutcome outcome)
	{
		CompanionSetup companion = IsUsable(speaker) ? speaker : CompanionChatAI.PickSpeaker();
		if (companion == null)
		{
			outcome.Message = ModLocalization.Loc("hc_ai_no_companion");
			CompanionsPlugin.Log?.LogInfo("[IA] Order \"inventory\": no companion to open (none of yours is in range, or their Command switch is off)");
			return outcome;
		}
		Container container = companion.GetComponent<Container>();
		if (container == null)
		{
			outcome.Message = ModLocalization.LocFmt("hc_ai_no_container", NameOf(companion));
			CompanionsPlugin.Log?.LogInfo($"[IA] Order \"inventory\": {NameOf(companion)} has no Container - nothing to open");
			return outcome;
		}
		switch (CompanionInteract.RequestCompanionInventory(companion, container, player))
		{
		case CompanionInteract.InventoryOpenResult.Opened:
			outcome.Count = 1;
			outcome.Message = ModLocalization.Loc("hc_ai_inventory_open");
			companion.GetComponent<CompanionTalk>()?.Say(outcome.Message, true);
			CompanionsPlugin.Log?.LogInfo($"[IA] Order \"inventory\": {NameOf(companion)}'s window is open");
			break;
		case CompanionInteract.InventoryOpenResult.Requesting:
			// the ownership was asked for; the window opens on its own as soon as it arrives
			outcome.Count = 1;
			outcome.Message = ModLocalization.LocFmt("hc_ai_inventory_requesting", NameOf(companion));
			CompanionsPlugin.Log?.LogInfo($"[IA] Order \"inventory\": {NameOf(companion)}'s container isn't ours - ownership requested, the window opens when it arrives");
			break;
		case CompanionInteract.InventoryOpenResult.NoGui:
			// InventoryGui.instance == null: no world yet (the answer landed while the game was still loading)
			outcome.Message = ModLocalization.Loc("hc_ai_inventory_no_gui");
			CompanionsPlugin.Log?.LogInfo("[IA] Order \"inventory\": no InventoryGui in this scene - nothing was opened");
			break;
		default:
			outcome.Message = ModLocalization.LocFmt("hc_ai_no_container", NameOf(companion));
			CompanionsPlugin.Log?.LogInfo("[IA] Order \"inventory\": the container could not be opened");
			break;
		}
		return outcome;
	}

	/// <summary>"fecha o inventário" (bonus of Fase 3.8): closes the window the mod opened. The companion panel hides
	/// itself on the next frame - its own Update watches the game's container - so only the game's window is closed
	/// here, which is exactly what ESC does.</summary>
	private static OrderOutcome CloseInventory(OrderOutcome outcome)
	{
		InventoryGui gui = InventoryGui.instance;
		if (gui == null || !InventoryGui.IsVisible())
		{
			outcome.Message = ModLocalization.Loc("hc_ai_inventory_none_open");
			CompanionsPlugin.Log?.LogInfo("[IA] Order \"inventory_close\": no inventory window was open");
			return outcome;
		}
		gui.Hide();
		outcome.Count = 1;
		outcome.Message = ModLocalization.Loc("hc_ai_inventory_closed");
		CompanionsPlugin.Log?.LogInfo("[IA] Order \"inventory_close\": window closed");
		return outcome;
	}

	/// <summary>Fase 3.8: can this companion still be handed an order? (still ours, still there). Read without taking
	/// it over, exactly like the speaker pick does.</summary>
	private static bool IsUsable(CompanionSetup companion)
	{
		return companion != null && MHR_Owner.IsMine(companion) && companion.GetComponent<ZNetView>()?.GetZDO() != null;
	}

	/// <summary>
	/// Fase 3.8: does the player's OWN message ask for the companion's inventory? The fallback for the model that
	/// files "abre o teu inventário" as conversation ("none" + a reply about the pack) and leaves the player talking
	/// to a window that never opens - the same safety net of Fase 3.6b, for the one order that is unmistakable in
	/// the words themselves.
	///
	/// Deliberately narrow: the message has to carry a word for the pack (inventário/mochila/bolsa/bag, or "carrega"
	/// in "mostra o que você carrega") AND a verb of opening or closing, so "o que tem no teu inventário?" and
	/// "quanto pesa a tua mochila?" stay conversation, and "abre a porta" is not an inventory either.
	/// </summary>
	/// <param name="close">true when the message asks to CLOSE the window instead of opening it.</param>
	internal static bool IsInventoryRequest(string message, out bool close)
	{
		close = false;
		if (string.IsNullOrEmpty(message))
		{
			return false;
		}
		string text = LlmIntent.Normalize(message);
		if (text.Length == 0)
		{
			return false;
		}
		bool pack = false;
		bool verb = false;
		string[] words = text.Split('_');
		for (int i = 0; i < words.Length; i++)
		{
			string word = words[i];
			if (PackWords.Contains(word))
			{
				pack = true;
			}
			else if (CloseWords.Contains(word))
			{
				verb = true;
				close = true;
			}
			else if (OpenWords.Contains(word))
			{
				verb = true;
			}
		}
		return pack && verb;
	}

	/// <summary>The words that name the companion's pack ("mostra o que você carrega").</summary>
	private static readonly HashSet<string> PackWords = new HashSet<string>(StringComparer.Ordinal)
	{
		"inventario", "inventarios", "invent", "inventory", "mochila", "bolsa", "bag", "carrega", "carregas"
	};

	/// <summary>The verbs that ask to SEE it.</summary>
	private static readonly HashSet<string> OpenWords = new HashSet<string>(StringComparer.Ordinal)
	{
		"abre", "abrir", "abra", "abram", "mostra", "mostrar", "mostre", "olha", "olhar", "olhe", "exibe", "exibir",
		"ver", "veja", "ve", "open", "show"
	};

	/// <summary>The verbs that ask to CLOSE it.</summary>
	private static readonly HashSet<string> CloseWords = new HashSet<string>(StringComparer.Ordinal)
	{
		"fecha", "fechar", "feche", "fechem", "esconde", "esconder", "esconda", "close"
	};

	// ---- Fase 3.6b: what to act on when the model named nothing ----

	/// <summary>
	/// Fase 3.6b: the item an equip/drop order is about.
	///
	/// The model's "target" wins whenever it really answers something a companion carries. When it doesn't - the
	/// reported bug: "target": null, with the item right there in the player's sentence - the PLAYER'S OWN WORDS are
	/// read instead: <see cref="OrderTargets.Phrases"/> turns "Joga 10 pedras no chão" into pedras (the order's
	/// verbs, pronouns, numbers and places are dropped) and every phrase is matched against the packs with the very
	/// same matcher the rest of Fase 3.6 uses, only with the higher bar of
	/// <see cref="OrderTargets.MinPhraseScore"/>. Nothing is invented: a phrase that answers no item is refused, and
	/// when even that fails the model's own name is handed back so the player keeps the old warning ("Não entendi
	/// qual item." with no name at all, "Não tenho X." with one).
	/// </summary>
	/// <param name="action">The canonical action, for the log.</param>
	private static string ResolveItemTarget(string modelTarget, string message, string action)
	{
		if (ItemMatchInPacks(modelTarget) >= OrderTargets.MinScore)
		{
			return modelTarget;
		}
		List<string> phrases = OrderTargets.Phrases(message);
		for (int i = 0; i < phrases.Count; i++)
		{
			int score = ItemMatchInPacks(phrases[i]);
			if (score < OrderTargets.MinPhraseScore)
			{
				continue;
			}
			CompanionsPlugin.Log?.LogInfo($"[IA] Order \"{action}\": the model named "
				+ (string.IsNullOrEmpty(modelTarget) ? "no item" : $"\"{modelTarget}\", which no companion carries")
				+ $" - the player's own words named \"{phrases[i]}\" (match {score}, Fase 3.6b fallback)");
			return phrases[i];
		}
		return modelTarget;
	}

	/// <summary>How well a name answers something the companions actually carry, 0 when nobody has it. Only the
	/// packs are looked at (never chests), and only companions that could take an order at all - the ownership and
	/// Command checks are read without taking the companion over (unlike MHR_InScope, which does).</summary>
	private static int ItemMatchInPacks(string target)
	{
		if (string.IsNullOrEmpty(target))
		{
			return 0;
		}
		int best = 0;
		IReadOnlyList<CompanionSetup> all = CompanionSetup.AllCompanions;
		for (int i = 0; i < all.Count; i++)
		{
			CompanionSetup companion = all[i];
			if (companion == null || !MHR_Owner.IsMine(companion) || !companion.GetIsCommandable())
			{
				continue;
			}
			Humanoid humanoid = companion.GetComponent<Humanoid>();
			if (humanoid == null)
			{
				continue;
			}
			ItemDrop.ItemData item = OrderTargets.FindItem(humanoid, target, out int score);
			if (item != null && score > best)
			{
				best = score;
			}
		}
		return best;
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
			SetFollow(companion, true);
			return true;
		case "stop":
			CompanionSetup.ClearOrderCap(companion);
			// "para de me seguir" must actually STOP the follow - the wheel's Follow toggle turns it off the same
			// way. Turning it on here (as it used to) made the order look like it did nothing.
			SetFollow(companion, false);
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
			// Fase 3.1: até aqui a ordem era só um flag (o ímã e o MHR_Loot pulam de propósito o que foi largado, e
			// nada mais no mod andava até um item no chão), então o companheiro agora vai mesmo catar o que está
			// espalhado por perto - ver MHR_Collect.
			MHR_Collect.Start(companion);
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

	// "follow" turns following ON; "stop" turns it OFF (exactly what the wheel's Follow toggle does). Either way
	// the job is dropped first: a job and Follow can't both be on, and the job that had paused Follow must not
	// turn it back on later (same as the wheel does it).
	private static void SetFollow(CompanionSetup companion, bool follow)
	{
		MHR_JobRules.FollowSetByPlayer(companion);
		companion.SetFollow(follow);
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
