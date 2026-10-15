using System;
using HarmonyLib;
using UnityEngine;

namespace Companions;

/// <summary>
/// Fase 3.1: the player types an order in the game chat ("colete 20 de madeira", "segue", "para") and a local LLM
/// turns it into a companion order. The chat text is captured with a postfix on Chat.SendText (the only place the
/// local player's own message passes through - incoming messages go to Chat.OnNewChatMessage instead), the request
/// runs on a background task and the answer is applied on the main thread in Update().
/// </summary>
internal static class CompanionChatAI
{
	/// <summary>The instruction sent with every order. Kept in Portuguese: the player types Portuguese in this mod.</summary>
	internal const string SystemPrompt = "Você é o tradutor de ordens de um mod de companheiros do Valheim.\n"
		+ "A mensagem do usuário é o que o JOGADOR falou para o companheiro NPC no chat do jogo.\n"
		+ "Responda SOMENTE com um JSON compacto, em uma linha, sem markdown e sem nenhum texto em volta.\n"
		+ "Formato exato: {\"action\":\"<acao>\",\"target\":\"<alvo ou null>\",\"resource\":\"<recurso ou null>\",\"amount\":<numero ou null>}\n"
		+ "Ações válidas:\n"
		+ "follow (seguir o jogador), wood (coletar madeira/derrubar árvores), stone (coletar pedra), ore (minerar minério),\n"
		+ "forage (colher plantas, frutas, cogumelos), smelt (fundir minério no forno), hunt (caçar animais),\n"
		+ "attack (atacar/matar UMA criatura citada pelo jogador), equip (equipar um item que o companheiro já tem),\n"
		+ "drop (largar/jogar fora um item que o companheiro já tem),\n"
		+ "farm (cuidar da plantação), fish (pescar), repair (reparar construções e reabastecer),\n"
		+ "cook (cozinhar comida), stop (parar o que está fazendo e voltar a seguir), come (vir até o jogador),\n"
		+ "sethome (definir a casa aqui), wander (vagar por perto), nowander (parar de vagar),\n"
		+ "pickup (recolher itens do chão), nopickup (parar de recolher),\n"
		+ "command (aceitar comandos), nocommand (ignorar comandos),\n"
		+ "stance_balanced, stance_passive (defensivo), stance_ranged (à distância), stance_melee (corpo a corpo),\n"
		+ "none (a mensagem NÃO é uma ordem para o companheiro: conversa, pergunta, saudação ou algo que você não entende).\n"
		+ "Regras:\n"
		+ "- \"target\" é a criatura ou o item citado pela ordem, com as MESMAS PALAVRAS do jogador, em português, SEM traduzir:\n"
		+ "  \"ataca aquele greydwarf\" -> \"greydwarf\"; \"equipa o machado de bronze\" -> \"machado de bronze\". Use null quando não citar nada.\n"
		+ "- \"resource\" é o recurso que o trabalho vai coletar, em inglês, quando houver (wood, stone, ore, raspberry); use null quando não houver.\n"
		+ "- \"amount\" é a quantidade pedida (\"colete 20 de madeira\" -> 20); use null quando o jogador não disser.\n"
		+ "- attack SÓ quando o jogador citar a criatura; \"atacar\"/\"matar\" sem criatura citada é hunt.\n"
		+ "- equip e drop valem só para o que o companheiro já carrega no inventário dele.\n"
		+ "- Use \"none\" sempre que não tiver certeza.\n"
		+ "Exemplos:\n"
		+ "\"colete 20 de madeira\" -> {\"action\":\"wood\",\"target\":null,\"resource\":\"wood\",\"amount\":20}\n"
		+ "\"vai pegar pedra\" -> {\"action\":\"stone\",\"target\":null,\"resource\":\"stone\",\"amount\":null}\n"
		+ "\"minere ferro\" -> {\"action\":\"ore\",\"target\":null,\"resource\":\"iron\",\"amount\":null}\n"
		+ "\"ataca aquele greydwarf\" -> {\"action\":\"attack\",\"target\":\"greydwarf\",\"resource\":null,\"amount\":null}\n"
		+ "\"mata o javali\" -> {\"action\":\"attack\",\"target\":\"javali\",\"resource\":null,\"amount\":null}\n"
		+ "\"ataca o troll\" -> {\"action\":\"attack\",\"target\":\"troll\",\"resource\":null,\"amount\":null}\n"
		+ "\"equipa o machado de bronze\" -> {\"action\":\"equip\",\"target\":\"machado de bronze\",\"resource\":null,\"amount\":null}\n"
		+ "\"pega a picareta\" -> {\"action\":\"equip\",\"target\":\"picareta\",\"resource\":null,\"amount\":null}\n"
		+ "\"larga a pedra\" -> {\"action\":\"drop\",\"target\":\"pedra\",\"resource\":null,\"amount\":null}\n"
		+ "\"joga fora 5 madeira\" -> {\"action\":\"drop\",\"target\":\"madeira\",\"resource\":null,\"amount\":5}\n"
		+ "\"segue\" -> {\"action\":\"follow\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"para\" -> {\"action\":\"stop\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"vem cá\" -> {\"action\":\"come\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"fica na defensiva\" -> {\"action\":\"stance_passive\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"bom dia, tudo bem?\" -> {\"action\":\"none\",\"target\":null,\"resource\":null,\"amount\":null}";

	private const int MaxRawShown = 400;

	private static float _lastOrderTime = -999f;

	/// <summary>Postfix on the player's own outgoing chat message.</summary>
	[HarmonyPatch(typeof(Chat), "SendText")]
	private static class ChatSendText_Patch
	{
		private static void Postfix(Talker.Type type, string text)
		{
			CompanionChatAI.OnPlayerChat(type, text);
		}
	}

	internal static void Init()
	{
		CompanionsPlugin.Log?.LogInfo($"[IA] Chat orders ready: enabled={ModConfig.AiEnabled?.Value ?? true}, "
			+ $"endpoint={LlmClient.Endpoint}, model=\"{ModConfig.AiModel?.Value}\", "
			+ $"prefix=\"{ModConfig.AiPrefix?.Value}\", timeout={ModConfig.AiTimeoutSeconds?.Value ?? 30}s");
	}

	/// <summary>Called every frame from the plugin's Update: applies finished orders. Never blocks, never allocates when idle.</summary>
	internal static void Tick()
	{
		try
		{
			while (LlmClient.TryDequeue(out LlmResult result))
			{
				LlmClient.MarkDone();
				HandleResult(result);
			}
			LlmClient.Tick();
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[IA] Tick error: " + ex);
		}
	}

	private static void OnPlayerChat(Talker.Type type, string text)
	{
		try
		{
			if (type == Talker.Type.Ping || string.IsNullOrEmpty(text))
			{
				return;
			}
			if (Player.m_localPlayer == null)
			{
				return;
			}
			string prefix = ModConfig.AiPrefix != null ? ModConfig.AiPrefix.Value : "";
			string message = text.Trim();
			if (!string.IsNullOrEmpty(prefix))
			{
				if (!message.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				{
					return;
				}
				message = message.Substring(prefix.Length).Trim();
				if (message.Length == 0)
				{
					return;
				}
			}
			if (ModConfig.AiEnabled == null || !ModConfig.AiEnabled.Value)
			{
				if (!string.IsNullOrEmpty(prefix))
				{
					Notify(ModLocalization.Loc("hc_ai_disabled"));
				}
				return;
			}
			float cooldown = ModConfig.AiCooldown != null ? ModConfig.AiCooldown.Value : 3f;
			float now = Time.realtimeSinceStartup;
			if (now - _lastOrderTime < cooldown)
			{
				CompanionsPlugin.Log?.LogInfo("[IA] Cooldown still running - message not sent to the model");
				return;
			}
			if (LlmClient.Busy)
			{
				CompanionsPlugin.Log?.LogInfo("[IA] The previous order is still being worked out - message not sent");
				return;
			}
			_lastOrderTime = now;
			Notify(ModLocalization.Loc("hc_ai_thinking"));
			LlmClient.Request(message, SystemPrompt);
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[IA] Couldn't handle the chat message: " + ex);
		}
	}

	private static void HandleResult(LlmResult result)
	{
		if (result == null)
		{
			return;
		}
		if (!result.Ok)
		{
			Notify(ModLocalization.LocFmt("hc_ai_error", result.Error ?? "?"));
			return;
		}
		if (ModConfig.AiShowResponse != null && ModConfig.AiShowResponse.Value && !string.IsNullOrEmpty(result.Content))
		{
			string shown = result.Content.Length > MaxRawShown ? result.Content.Substring(0, MaxRawShown) + "..." : result.Content;
			Notify(ModLocalization.LocFmt("hc_ai_raw", shown.Replace('\n', ' ').Replace('\r', ' ')));
		}

		LlmIntent intent = result.Intent;
		if (intent == null || intent.IsNone)
		{
			CompanionsPlugin.Log?.LogInfo($"[IA] Not an order (action \"{intent?.Action}\") - nothing done");
			Notify(ModLocalization.Loc("hc_ai_not_understood"));
			return;
		}

		CompanionOrders.OrderOutcome outcome = CompanionOrders.Run(intent);
		if (outcome == null || !outcome.Understood)
		{
			Notify(ModLocalization.Loc("hc_ai_not_understood"));
			return;
		}
		// Fase 3.6: attack/equip/drop have their own wording ("Atacando o troll.", "Não tenho picareta."), because
		// the feedback has to say WHICH creature or item it was about - the generic "Ok: ..." line can't.
		if (!string.IsNullOrEmpty(outcome.Message))
		{
			CompanionsPlugin.Log?.LogInfo($"[IA] \"{intent.Action}\"{(string.IsNullOrEmpty(intent.Target) ? "" : $" target=\"{intent.Target}\"")} -> {outcome.Count} companion(s) in {result.ElapsedMs} ms");
			Notify(outcome.Message);
			return;
		}
		if (outcome.Count == 0)
		{
			// Fase 3.2c: "colete 10 madeiras" with 15 already in the pack - the order was never started
			Notify(ModLocalization.Loc(outcome.AlreadyHave ? "hc_ai_already_have" : "hc_ai_no_companion"));
			return;
		}
		string label = ModLocalization.LocOr(outcome.LabelKey, intent.Action);
		string feedback = outcome.Amount > 0
			? ModLocalization.LocFmt("hc_ai_ok_amount", label, outcome.Amount)
			: ModLocalization.LocFmt("hc_ai_ok", label);
		CompanionsPlugin.Log?.LogInfo($"[IA] Applied \"{intent.Action}\" to {outcome.Count} companion(s) in {result.ElapsedMs} ms");
		Notify(feedback);
	}

	private static void Notify(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		try
		{
			MessageHud.instance?.ShowMessage(MessageHud.MessageType.TopLeft, text);
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogDebug("[IA] HUD message failed: " + ex.Message);
		}
		try
		{
			Chat.instance?.AddString(text);
		}
		catch (Exception ex2)
		{
			CompanionsPlugin.Log?.LogDebug("[IA] Chat message failed: " + ex2.Message);
		}
	}
}
