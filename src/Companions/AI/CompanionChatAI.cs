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
///
/// Fase 3.3 adds the VOICE source: the text the microphone produced (see VoiceInput/WhisperStt) is handed to the same
/// <see cref="Submit"/> the chat uses, so a spoken order is the same order - nothing is duplicated for it here. The
/// only voice-specific bits are the lines the pipeline itself shows ("Ouvindo...", "Entendi: ...") and the refusal
/// notice when the AI is off, on cooldown or still busy. Fase 3.3d: the "Entendi: ..." line is what the PLAYER said,
/// so its speech bubble goes under the player's head (CompanionTalk.SayAsPlayer) - the companion's own answers keep
/// speaking in their own bubble (Speak).
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
		+ "inventory (abrir o inventário do companheiro para o jogador ver o que ele carrega),\n"
		+ "farm (cuidar da plantação), fish (pescar), repair (reparar construções e reabastecer),\n"
		+ "cook (cozinhar comida), stop (parar o que está fazendo e voltar a seguir), come (vir até o jogador),\n"
		+ "sethome (definir a casa aqui), wander (vagar por perto), nowander (parar de vagar),\n"
		+ "pickup (recolher itens do chão), nopickup (parar de recolher),\n"
		+ "command (aceitar comandos), nocommand (ignorar comandos),\n"
		+ "stance_balanced, stance_passive (defensivo), stance_ranged (à distância), stance_melee (corpo a corpo),\n"
		+ "none (a mensagem NÃO é uma ordem para o companheiro: conversa, pergunta, saudação ou algo que você não entende).\n"
		+ "Regras:\n"
		+ "- \"reply\" é SEMPRE a sua fala, em português, no seu jeito de falar, e é UMA ÚNICA FRASE CURTA (até ~15 palavras,\n"
		+ "  sem duas frases, sem listas, sem parênteses): ela é FALADA EM VOZ ALTA, então uma frase curta sai na hora e\n"
		+ "  uma frase longa faz o jogador esperar. Numa ORDEM ela é a confirmação (\"Deixa comigo.\"); quando NÃO é ordem\n"
		+ "  é a sua resposta de conversa, respondendo de verdade o que o jogador perguntou.\n"
		+ "- \"target\" é OBRIGATÓRIO em attack, equip e drop: é o NOME da criatura ou do item citado pela ordem, com as MESMAS\n"
		+ "  PALAVRAS do jogador, em português, SEM traduzir e NUNCA null (nem vazio). \"ataca o greydwarf\" -> \"greydwarf\";\n"
		+ "  \"joga essas pedras que você pegou\" -> \"pedra\"; \"joga 10 pedras no chão\" -> \"pedra\"; \"larga a madeira\" -> \"madeira\";\n"
		+ "  \"equipa o machado de bronze\" -> \"machado de bronze\". Nas outras ações use null quando o jogador não citar nada.\n"
		+ "- \"resource\" é o recurso que o trabalho vai coletar, em inglês, quando houver (wood, stone, ore, raspberry); use null quando não houver.\n"
		+ "- \"amount\" é a quantidade pedida (\"colete 20 de madeira\" -> 20); use null quando o jogador não disser.\n"
		+ "- attack SÓ quando o jogador citar a criatura; \"atacar\"/\"matar\" sem criatura citada é hunt.\n"
		+ "- equip e drop valem só para o que o companheiro já carrega no inventário dele; o item vai SEMPRE em \"target\", nunca em \"resource\".\n"
		+ "- inventory quando o jogador pedir para VER o que o companheiro carrega (\"abre o teu inventário\", \"mostra o que você\n"
		+ "  carrega\"): você abre a sua mochila para ele, com as duas mãos - isso não é conversa.\n"
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
		+ "\"joga essas pedras que você pegou\" -> {\"action\":\"drop\",\"reply\":\"Largando as pedras.\",\"target\":\"pedra\",\"resource\":null,\"amount\":null}\n"
		+ "\"joga 10 pedras no chão\" -> {\"action\":\"drop\",\"reply\":\"Jogando fora.\",\"target\":\"pedra\",\"resource\":null,\"amount\":10}\n"
		+ "\"abre o teu inventário\" -> {\"action\":\"inventory\",\"reply\":\"Aqui está, chefe.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"mostra o que você carrega\" -> {\"action\":\"inventory\",\"reply\":\"Dá uma olhada.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
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
			// a chat message stays silent when the AI is off or the model is busy: only a message the player
			// deliberately addressed to the companion (with the prefix) says why nothing happened.
			Submit(message, "chat", announceRefusal: !string.IsNullOrEmpty(prefix));
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[IA] Couldn't handle the chat message: " + ex);
		}
	}

	/// <summary>
	/// Fase 3.3: the ONE place an order enters, whatever its source - the chat (Fase 3.1) or the microphone
	/// (<see cref="VoiceInput"/>). Everything from here on is identical for both: the cooldown, the companion being
	/// talked to, its personality, its memory, the prompt, the model call and the answer in Update(). The voice path
	/// adds nothing of its own, so an order spoken out loud behaves exactly like the same order typed.
	///
	/// Called on the main thread. Returns false when the order was refused; <paramref name="announceRefusal"/> makes it
	/// say why in the HUD and the chat (the voice path wants that - the player pressed a key and has to know what
	/// happened; a chat message does not, or an idle chat would fill up with warnings).
	/// </summary>
	internal static bool Submit(string message, string source, bool announceRefusal)
	{
		if (string.IsNullOrEmpty(message))
		{
			return false;
		}
		if (Player.m_localPlayer == null)
		{
			return false;
		}
		if (ModConfig.AiEnabled == null || !ModConfig.AiEnabled.Value)
		{
			CompanionsPlugin.Log?.LogInfo("[IA] The AI is off ([IA] Ligado = false) - message not sent");
			if (announceRefusal)
			{
				Notify(ModLocalization.Loc("hc_ai_disabled"));
			}
			return false;
		}
		float cooldown = ModConfig.AiCooldown != null ? ModConfig.AiCooldown.Value : 3f;
		float now = Time.realtimeSinceStartup;
		if (now - _lastOrderTime < cooldown)
		{
			CompanionsPlugin.Log?.LogInfo($"[IA] Cooldown still running ({now - _lastOrderTime:0.0}s of {cooldown:0.0}s) - message not sent to the model");
			if (announceRefusal)
			{
				Notify(ModLocalization.Loc("hc_ai_voice_busy"));
			}
			return false;
		}
		if (LlmClient.Busy)
		{
			CompanionsPlugin.Log?.LogInfo("[IA] The previous order is still being worked out - message not sent");
			if (announceRefusal)
			{
				Notify(ModLocalization.Loc("hc_ai_voice_busy"));
			}
			return false;
		}
		_lastOrderTime = now;
		CompanionsPlugin.Log?.LogInfo($"[IA] Order from {source}: \"{message}\"");

		// Fase 3.5: the companion being talked to is the nearest one that can take an order. It is NOT taken over
		// here (that is what MHR_InScope does for orders): a message must not change who runs the companion.
		CompanionSetup speaker = PickSpeaker();
		_pendingSpeaker = speaker;
		_pendingName = NameOf(speaker);
		_pendingKey = KeyFor(speaker, _pendingName);

		Notify(ModLocalization.Loc("hc_ai_thinking"));
		LlmClient.Request(message, BuildPrompt(_pendingName, CompanionPersona.Resolve(), _pendingKey));
		return true;
	}

	/// <summary>
	/// Fase 3.3: a line the voice pipeline has to show (listening, no microphone, an error) - same HUD + chat feedback
	/// the orders get, so everything the companion "hears" is visible in one place.
	/// </summary>
	internal static void Announce(string text)
	{
		Notify(text);
	}

	/// <summary>
	/// Fase 3.3: shows what the microphone understood ("Entendi: ...") in the HUD and the chat. Fase 3.3d: the line
	/// itself is the PLAYER's, so the speech bubble goes under the player's own head (CompanionTalk.SayAsPlayer) -
	/// before this the nearest companion repeated it, which put the player's words over the companion. The
	/// companion's own answers are unaffected: they still speak in their own bubble (see Speak). Called on the main
	/// thread, once per transcription.
	/// </summary>
	internal static void HeardByVoice(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		Notify(ModLocalization.LocFmt("hc_ai_voice_heard", text));
		Player player = Player.m_localPlayer;
		if (player != null)
		{
			CompanionTalk.SayAsPlayer(player.gameObject, text);
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
		builder.Append("Fale sempre em português, no seu jeito, com UMA frase curta, e trate o jogador como seu dono.\n");
		builder.Append("A sua fala é lida em voz alta para o jogador: uma frase curta sai na hora, uma frase longa faz ele esperar.\n");
		builder.Append(Contract);
		CompanionMemory.AppendTo(builder, memoryKey, name);
		return builder.ToString();
	}

	/// <summary>Fase 3.8: the companion the message in flight was addressed to (chosen by <see cref="PickSpeaker"/>
	/// when the message was sent). Read when the answer arrives, so an order that has to pick ONE companion - the
	/// inventory, which can only be one window - uses the very same one the player was talking to.</summary>
	internal static CompanionSetup PendingSpeaker => _pendingSpeaker;

	/// <summary>
	/// The companion the player is talking to: the nearest one of theirs that can take an order and is within the
	/// recall range (the same leash orders use). Null when there is none - the answer still comes back in the chat,
	/// just without a speech bubble. Runs once per chat message, so the walk over the companions is free.
	/// Fase 3.8: also used by CompanionOrders when the speaker of the message is gone by the time the answer lands.
	/// </summary>
	internal static CompanionSetup PickSpeaker()
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
		// Fase 3.8: a small model files "abre o teu inventário" as conversation ("none" plus a reply about the pack),
		// and the player was left talking to a window that never opened. The player's own words are read for that one
		// order when the model understood nothing else - CompanionOrders.IsInventoryRequest is deliberately narrow
		// (an open/close verb AND a word for the pack), so "o que tem no teu inventário?" stays a conversation.
		if (intent != null && intent.IsNone && CompanionOrders.IsInventoryRequest(result.UserText, out bool closeInventory))
		{
			CompanionsPlugin.Log?.LogInfo($"[IA] The model answered \"{intent.Action}\" for \"{result.UserText}\", which asks for the inventory - taking it as the {(closeInventory ? "close" : "open")} order (Fase 3.8 fallback)");
			intent.Action = closeInventory ? "inventory_close" : "inventory";
			intent.Target = null;
			intent.Resource = null;
		}
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
				// Fase 3.4: the same line is spoken (the reply is the whole line of a conversation, so there is no
				// "mod line" here to win over it)
				SpeakOutLoud(_pendingSpeaker, null, line);
				spoken = line;
			}
			else
			{
				CompanionsPlugin.Log?.LogInfo($"[IA] Not an order (action \"{intent?.Action}\") - nothing done");
				string line2 = ModLocalization.Loc("hc_ai_not_understood");
				Notify(line2);
				SpeakOutLoud(_pendingSpeaker, line2, reply);
			}
		}
		else
		{
			// Fase 3.6: attack/equip/drop have their own wording ("Atacando o troll.", "Não tenho picareta."), because
			// the feedback has to say WHICH creature or item it was about - the generic "Ok: ..." line can't.
			CompanionOrders.OrderOutcome outcome = CompanionOrders.Run(intent, result.UserText, _pendingSpeaker);
			if (outcome == null || !outcome.Understood)
			{
				string line3 = ModLocalization.Loc("hc_ai_not_understood");
				Notify(line3);
				SpeakOutLoud(_pendingSpeaker, line3, reply);
			}
			else if (!string.IsNullOrEmpty(outcome.Message))
			{
				CompanionsPlugin.Log?.LogInfo($"[IA] \"{intent.Action}\"{(string.IsNullOrEmpty(intent.Target) ? "" : $" target=\"{intent.Target}\"")} -> {outcome.Count} companion(s) in {result.ElapsedMs} ms");
				Notify(outcome.Message);
				SpeakOutLoud(_pendingSpeaker, outcome.Message, reply);
				spoken = outcome.Message;
			}
			else if (outcome.Count == 0)
			{
				// Fase 3.2c: "colete 10 madeiras" with 15 already in the pack - the order was never started
				string line4 = ModLocalization.Loc(outcome.AlreadyHave ? "hc_ai_already_have" : "hc_ai_no_companion");
				Notify(line4);
				SpeakOutLoud(_pendingSpeaker, line4, reply);
			}
			else
			{
				string label = ModLocalization.LocOr(outcome.LabelKey, intent.Action);
				string feedback = outcome.Amount > 0
					? ModLocalization.LocFmt("hc_ai_ok_amount", label, outcome.Amount)
					: ModLocalization.LocFmt("hc_ai_ok", label);
				CompanionsPlugin.Log?.LogInfo($"[IA] Applied \"{intent.Action}\" to {outcome.Count} companion(s) in {result.ElapsedMs} ms");
				Notify(feedback);
				SpeakOutLoud(_pendingSpeaker, feedback, reply);
				spoken = feedback;
			}
			// Fase 3.5: for an ORDER the mod's own line wins and the model's "reply" is NOT shown - the companion would
			// otherwise say two different things about the same order (and the localized line names the creature/item,
			// which the free text can't). Fase 3.4: the same rule decides what is SPOKEN (SpeakOutLoud) - so with both
			// TtsFalarFalasDoMod and TtsFalarRespostas on, the visible line is the one that goes to the voice.
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

	/// <summary>
	/// Fase 3.4: the companion says it in <b>voice</b> (local TTS, see CompanionTts). The rule is "the voice follows
	/// the line that is shown in the chat":
	/// <list type="bullet">
	/// <item><paramref name="modLine"/> is the mod's own localized feedback (the one in the chat/HUD) and is spoken
	/// when <c>[IA] TtsFalarFalasDoMod</c> is on;</item>
	/// <item><paramref name="reply"/> is the model's own wording and is spoken when there is no line to read or when
	/// <c>[IA] TtsFalarFalasDoMod</c> is off, and only with <c>[IA] TtsFalarRespostas</c> on.</item>
	/// </list>
	/// Only one of the two is ever spoken (the other goes to the log), so an order is never confirmed twice in two
	/// different ways. Called on the main thread; the synthesis itself happens on a background thread.
	/// </summary>
	private static void SpeakOutLoud(CompanionSetup companion, string modLine, string reply)
	{
		string text = PickSpokenText(CompanionTts.SpeakModLines, CompanionTts.SpeakReplies, modLine, reply);
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		if (!string.IsNullOrEmpty(modLine) && !string.IsNullOrEmpty(reply) && !string.Equals(modLine, reply, StringComparison.Ordinal))
		{
			string other = string.Equals(text, modLine, StringComparison.Ordinal) ? reply : modLine;
			CompanionsPlugin.Log?.LogInfo($"[IA] Voz (Fase 3.4): falando \"{Shorten(text, MaxReplyShown)}\"; o outro texto (\"{Shorten(other, MaxReplyShown)}\") fica so no log - nada e falado duas vezes.");
		}
		// Only the mod's own lines go into the TTS cache (CompanionTts): they repeat word for word, which is what
		// the cache is for. A reply from the model is new every time, and caching those would push the fixed lines
		// out of the cache (and grow the heap) on every single message.
		bool fromModel = !string.IsNullOrEmpty(reply) && string.Equals(text, reply, StringComparison.Ordinal) && !string.Equals(modLine, reply, StringComparison.Ordinal);
		CompanionTts.Speak(companion, text, !fromModel);
	}

	/// <summary>
	/// The whole "what is spoken" rule, on its own (no Unity, no config): the visible line wins when
	/// <paramref name="speakModLines"/> is on, otherwise the model's own wording, and only with
	/// <paramref name="speakReplies"/>. Null means "say nothing". Kept pure so the decision table is easy to reason
	/// about - and to check outside the game.
	/// </summary>
	internal static string PickSpokenText(bool speakModLines, bool speakReplies, string modLine, string reply)
	{
		if (!string.IsNullOrEmpty(modLine) && speakModLines)
		{
			return modLine;
		}
		if (!string.IsNullOrEmpty(reply) && speakReplies)
		{
			return reply;
		}
		return null;
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
