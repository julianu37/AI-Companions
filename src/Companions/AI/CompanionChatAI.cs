using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace Companions;

/// <summary>
/// Fase 3.1: the player types an order in the game chat ("colete 20 de madeira", "segue", "para") and a local LLM
/// turns it into a companion order. The chat text is captured with a postfix on Chat.SendText (the only place the
/// local player's own message passes through - incoming messages go to Chat.OnNewChatMessage instead), the request
/// runs on a background task and the answer is applied on the main thread in Update().
///
/// Fase 3.5 adds three things on top of that, all in the same request:
/// - PERSONALITY: the companion's name and its personality (a preset or the player's own text, see CompanionPersona)
///   go into the instructions, so the model answers in that tone.
/// - MEMORY: the last [IA] Memoria exchanges of that companion are pasted into the instructions (CompanionMemory),
///   so it follows the thread of the conversation.
/// - FREE CONVERSATION: when the message is not an order ("bom dia", "como você está?"), the model's "reply" is
///   spoken by the companion (speech bubble) and shown in the chat. An order keeps working exactly as before and
///   the mod's own, localized feedback line wins - the model's "reply" is only logged for orders, so nothing is said
///   twice (and the confirmation for attack/equip/drop still names the creature/item, which a free text could not).
/// </summary>
internal static class CompanionChatAI
{
	/// <summary>
	/// The instruction sent with every message: who the model is talking to, the JSON contract and the actions it may
	/// answer with. Kept in Portuguese: the player types Portuguese in this mod. The companion's name, its personality
	/// and the recent conversation are added in front of/behind it by BuildPrompt - everything here is fixed, so the
	/// beginning of the prompt stays cacheable by the server.
	/// </summary>
	internal const string Contract = "Você é um companheiro NPC de Valheim e o jogador é o seu DONO: você obedece a ele.\n"
		+ "A mensagem do usuário é o que o JOGADOR falou para você no chat do jogo.\n"
		+ "Responda SOMENTE com um JSON compacto, em uma linha, sem markdown e sem nenhum texto em volta.\n"
		+ "Formato exato: {\"action\":\"<acao>\",\"reply\":\"<o que voce diz ao jogador>\",\"target\":\"<alvo ou null>\",\"resource\":\"<recurso ou null>\",\"amount\":<numero ou null>}\n"
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
		+ "- \"reply\" é SEMPRE a sua fala, em português, curta (uma frase), no seu jeito de falar: numa ORDEM é a confirmação\n"
		+ "  (\"Deixa comigo.\"); quando NÃO é ordem é a sua resposta de conversa, respondendo de verdade o que o jogador perguntou.\n"
		+ "- \"target\" é a criatura ou o item citado pela ordem, com as MESMAS PALAVRAS do jogador, em português, SEM traduzir:\n"
		+ "  \"ataca aquele greydwarf\" -> \"greydwarf\"; \"equipa o machado de bronze\" -> \"machado de bronze\". Use null quando não citar nada.\n"
		+ "- \"resource\" é o recurso que o trabalho vai coletar, em inglês, quando houver (wood, stone, ore, raspberry); use null quando não houver.\n"
		+ "- \"amount\" é a quantidade pedida (\"colete 20 de madeira\" -> 20); use null quando o jogador não disser.\n"
		+ "- attack SÓ quando o jogador citar a criatura; \"atacar\"/\"matar\" sem criatura citada é hunt.\n"
		+ "- equip e drop valem só para o que o companheiro já carrega no inventário dele.\n"
		+ "- Use \"none\" sempre que não tiver certeza: conversa, dúvida ou pedido que não é uma das ações acima.\n"
		+ "Exemplos:\n"
		+ "\"colete 20 de madeira\" -> {\"action\":\"wood\",\"reply\":\"Vou buscar madeira.\",\"target\":null,\"resource\":\"wood\",\"amount\":20}\n"
		+ "\"vai pegar pedra\" -> {\"action\":\"stone\",\"reply\":\"Deixa comigo.\",\"target\":null,\"resource\":\"stone\",\"amount\":null}\n"
		+ "\"minere ferro\" -> {\"action\":\"ore\",\"reply\":\"Já vou minerar.\",\"target\":null,\"resource\":\"iron\",\"amount\":null}\n"
		+ "\"ataca aquele greydwarf\" -> {\"action\":\"attack\",\"reply\":\"Atacando!\",\"target\":\"greydwarf\",\"resource\":null,\"amount\":null}\n"
		+ "\"mata o javali\" -> {\"action\":\"attack\",\"reply\":\"Pode deixar.\",\"target\":\"javali\",\"resource\":null,\"amount\":null}\n"
		+ "\"ataca o troll\" -> {\"action\":\"attack\",\"reply\":\"Vou acabar com ele.\",\"target\":\"troll\",\"resource\":null,\"amount\":null}\n"
		+ "\"equipa o machado de bronze\" -> {\"action\":\"equip\",\"reply\":\"Machado na mão.\",\"target\":\"machado de bronze\",\"resource\":null,\"amount\":null}\n"
		+ "\"pega a picareta\" -> {\"action\":\"equip\",\"reply\":\"Peguei a picareta.\",\"target\":\"picareta\",\"resource\":null,\"amount\":null}\n"
		+ "\"larga a pedra\" -> {\"action\":\"drop\",\"reply\":\"Largando.\",\"target\":\"pedra\",\"resource\":null,\"amount\":null}\n"
		+ "\"joga fora 5 madeira\" -> {\"action\":\"drop\",\"reply\":\"Jogando fora.\",\"target\":\"madeira\",\"resource\":null,\"amount\":5}\n"
		+ "\"segue\" -> {\"action\":\"follow\",\"reply\":\"Estou com você.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"para\" -> {\"action\":\"stop\",\"reply\":\"Parando.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"vem cá\" -> {\"action\":\"come\",\"reply\":\"Já vou.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"fica na defensiva\" -> {\"action\":\"stance_passive\",\"reply\":\"Fico na defensiva.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"bom dia, tudo bem?\" -> {\"action\":\"none\",\"reply\":\"Bom dia! Tudo bem, e você?\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"o que achou da minha casa?\" -> {\"action\":\"none\",\"reply\":\"Boa casa! Só falta uma fogueira acesa.\",\"target\":null,\"resource\":null,\"amount\":null}";

	private const int MaxRawShown = 400;

	/// <summary>Longest reply the companion will say out loud, so a runaway model can't fill the screen.</summary>
	private const int MaxReplyShown = 300;

	private static float _lastOrderTime = -999f;

	// The request in flight and the conversation it belongs to. Only one request is in flight at a time (LlmClient),
	// so one set of fields is enough - they are written when the message is sent and read when the answer arrives,
	// both on the main thread.
	private static string _pendingKey = CompanionMemory.PlayerKey;

	private static string _pendingName = "";

	private static CompanionSetup _pendingSpeaker;

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
		// Fase 3.5: personality, memory and free conversation
		CompanionsPlugin.Log?.LogInfo($"[IA] Personality: {CompanionPersona.Describe()} (presets: {CompanionPersona.Ids()}); "
			+ $"memory: {CompanionMemory.Exchanges} exchange(s) per companion; conversation: {ConversationEnabled}");
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

	/// <summary>Fase 3.5: [IA] Conversa - answer free conversation, not only orders.</summary>
	private static bool ConversationEnabled => ModConfig.AiConversation == null || ModConfig.AiConversation.Value;

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

			// Fase 3.5: the companion being talked to is the nearest one that can take an order. It is NOT taken over
			// here (that is what MHR_InScope does for orders): a chat message must not change who runs the companion.
			CompanionSetup speaker = PickSpeaker();
			_pendingSpeaker = speaker;
			_pendingName = NameOf(speaker);
			_pendingKey = KeyFor(speaker, _pendingName);

			Notify(ModLocalization.Loc("hc_ai_thinking"));
			LlmClient.Request(message, BuildPrompt(_pendingName, CompanionPersona.Resolve(), _pendingKey));
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[IA] Couldn't handle the chat message: " + ex);
		}
	}

	/// <summary>
	/// The instructions for this message: who the companion is, how it talks, the fixed contract and (Fase 3.5) the
	/// recent conversation. Built on the main thread, once per chat message - never per frame.
	/// </summary>
	private static string BuildPrompt(string companionName, string personality, string memoryKey)
	{
		string name = string.IsNullOrEmpty(companionName) ? ModLocalization.Loc("hc_ai_default_name") : companionName;
		StringBuilder builder = new StringBuilder(Contract.Length + 320);
		builder.Append("Você é ").Append(name).Append(", o companheiro NPC do jogador, e ele é o seu dono.\n");
		builder.Append("Seu jeito de falar e de agir: ").Append(personality).Append('\n');
		builder.Append("Fale sempre em português, no seu jeito, com uma ou duas frases, e trate o jogador como seu dono.\n");
		builder.Append(Contract);
		CompanionMemory.AppendTo(builder, memoryKey, name);
		return builder.ToString();
	}

	/// <summary>
	/// The companion the player is talking to: the nearest one of theirs that can take an order and is within the
	/// recall range (the same leash orders use). Null when there is none - the answer still comes back in the chat,
	/// just without a speech bubble. Runs once per chat message, so the walk over the companions is free.
	/// </summary>
	private static CompanionSetup PickSpeaker()
	{
		Player player = Player.m_localPlayer;
		if (player == null)
		{
			return null;
		}
		float range = ModConfig.RecallRange != null ? ModConfig.RecallRange.Value : 40f;
		float limit = range > 0f ? range * range : 0f;
		Vector3 from = player.transform.position;
		CompanionSetup best = null;
		float bestSqr = float.MaxValue;
		IReadOnlyList<CompanionSetup> all = CompanionSetup.AllCompanions;
		for (int i = 0; i < all.Count; i++)
		{
			CompanionSetup companion = all[i];
			if (companion == null || !companion.GetIsCommandable() || !MHR_Owner.IsMine(companion))
			{
				continue;
			}
			Vector3 at = companion.transform.position;
			float dx = at.x - from.x;
			float dy = at.y - from.y;
			float dz = at.z - from.z;
			float sqr = dx * dx + dy * dy + dz * dz;
			if (limit > 0f && sqr > limit)
			{
				continue;
			}
			if (sqr < bestSqr)
			{
				bestSqr = sqr;
				best = companion;
			}
		}
		return best;
	}

	/// <summary>The conversation a companion's memory lives in: its permanent companion ID when it has one, its name
	/// otherwise (a companion that was never given an ID yet), and a shared bucket when nobody is nearby.</summary>
	private static string KeyFor(CompanionSetup companion, string name)
	{
		string id = companion != null ? companion.MHR_ResolveCompanionId() : "";
		if (!string.IsNullOrEmpty(id))
		{
			return id;
		}
		return string.IsNullOrEmpty(name) ? CompanionMemory.PlayerKey : "name:" + name;
	}

	/// <summary>The companion's name, for the prompt ("Thorin: ..." in the conversation).</summary>
	private static string NameOf(CompanionSetup companion)
	{
		ZDO zdo = companion != null ? companion.GetComponent<ZNetView>()?.GetZDO() : null;
		return zdo != null ? zdo.GetString(CompanionSetup.NameHash, "") : "";
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
		string reply = intent != null ? intent.Reply : null;
		// what the companion actually said (or did), kept in its memory so it can follow the thread
		string spoken = null;

		if (intent == null || intent.IsNone)
		{
			// Fase 3.5: not an order - the companion answers as conversation, out loud and in the chat
			if (ConversationEnabled && !string.IsNullOrEmpty(reply))
			{
				string line = Shorten(reply, MaxReplyShown);
				CompanionsPlugin.Log?.LogInfo($"[IA] Conversation ({result.ElapsedMs} ms): \"{line}\"");
				Speak(_pendingSpeaker, line);
				Notify(line);
				spoken = line;
			}
			else
			{
				CompanionsPlugin.Log?.LogInfo($"[IA] Not an order (action \"{intent?.Action}\") - nothing done");
				Notify(ModLocalization.Loc("hc_ai_not_understood"));
			}
		}
		else
		{
			// Fase 3.6: attack/equip/drop have their own wording ("Atacando o troll.", "Não tenho picareta."), because
			// the feedback has to say WHICH creature or item it was about - the generic "Ok: ..." line can't.
			CompanionOrders.OrderOutcome outcome = CompanionOrders.Run(intent);
			if (outcome == null || !outcome.Understood)
			{
				Notify(ModLocalization.Loc("hc_ai_not_understood"));
			}
			else if (!string.IsNullOrEmpty(outcome.Message))
			{
				CompanionsPlugin.Log?.LogInfo($"[IA] \"{intent.Action}\"{(string.IsNullOrEmpty(intent.Target) ? "" : $" target=\"{intent.Target}\"")} -> {outcome.Count} companion(s) in {result.ElapsedMs} ms");
				Notify(outcome.Message);
				spoken = outcome.Message;
			}
			else if (outcome.Count == 0)
			{
				// Fase 3.2c: "colete 10 madeiras" with 15 already in the pack - the order was never started
				Notify(ModLocalization.Loc(outcome.AlreadyHave ? "hc_ai_already_have" : "hc_ai_no_companion"));
			}
			else
			{
				string label = ModLocalization.LocOr(outcome.LabelKey, intent.Action);
				string feedback = outcome.Amount > 0
					? ModLocalization.LocFmt("hc_ai_ok_amount", label, outcome.Amount)
					: ModLocalization.LocFmt("hc_ai_ok", label);
				CompanionsPlugin.Log?.LogInfo($"[IA] Applied \"{intent.Action}\" to {outcome.Count} companion(s) in {result.ElapsedMs} ms");
				Notify(feedback);
				spoken = feedback;
			}
			// Fase 3.5: for an ORDER the mod's own line wins and the model's "reply" is NOT shown - the companion would
			// otherwise say two different things about the same order (and the localized line names the creature/item,
			// which the free text can't). The reply is only remembered, so the tone of the order still reaches the
			// memory of the next exchange.
			if (!string.IsNullOrEmpty(reply))
			{
				CompanionsPlugin.Log?.LogInfo($"[IA] Model's reply for the order (not shown, the mod's own line wins): \"{Shorten(reply, MaxReplyShown)}\"");
			}
		}

		if (spoken != null)
		{
			CompanionMemory.Remember(_pendingKey, fromPlayer: true, result.UserText);
			CompanionMemory.Remember(_pendingKey, fromPlayer: false, spoken);
			CompanionsPlugin.Log?.LogInfo($"[IA] Memory: {CompanionMemory.Count(_pendingKey)} message(s) kept for this companion");
		}
	}

	/// <summary>The companion says it out loud (speech bubble), forced: the player asked it directly.</summary>
	private static void Speak(CompanionSetup companion, string text)
	{
		if (companion == null || string.IsNullOrEmpty(text))
		{
			return;
		}
		try
		{
			CompanionTalk talk = companion.GetComponent<CompanionTalk>();
			if (talk != null)
			{
				talk.Say(text, force: true);
			}
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogDebug("[IA] Speech bubble failed: " + ex.Message);
		}
	}

	private static string Shorten(string text, int max)
	{
		if (string.IsNullOrEmpty(text))
		{
			return "";
		}
		string trimmed = text.Trim();
		return trimmed.Length > max ? trimmed.Substring(0, max) + "..." : trimmed;
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
