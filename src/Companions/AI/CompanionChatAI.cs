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
/// - MEMORY: the last [AI] Memory exchanges of that companion are pasted into the instructions (CompanionMemory),
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
	/// The name the "addressed" example of the contract uses. It is a placeholder on purpose - a name that reads
	/// like a character, not like a command - so the model learns the SHAPE of the field and not one of the player's
	/// real companions. A model that copies it can never block an order the player addressed by name: the player's
	/// own words win over the model's field (see HandleResult), and with no name in the message the rule and the
	/// example right after it say "null".
	/// </summary>
	private const string CompanionExampleName = "Borin";

	/// <summary>
	/// The instruction sent with every message, <b>in the language of the game</b> (Multilanguage M3 fix: it was
	/// Portuguese only, and in French "ramasse 10 bois" came back as <c>pickup</c> - the model had no example of a
	/// gathering order in its own language). The choice uses <see cref="CompanionLocale.Current"/> (the one table the
	/// TTS and the personality presets use), and any language without a contract of its own gets <b>English</b>.
	///
	/// <para>The <b>tokens</b> never change: the keys, the JSON shape and the 30 actions of
	/// <see cref="ValidActions"/> are the same in every language (the schema sent as <c>response_format</c> is built
	/// from them). What is translated are the rules and the examples - including, in every language, the pair that
	/// confuses the model: gathering with a number ("collect 10 wood") and picking up from the ground ("pick up the
	/// wood from the ground").</para>
	///
	/// <para>The four texts are <c>const</c>, so this property only picks a reference: nothing is built per message and
	/// nothing is cached by hand (the strings are immutable). The historical rules are all still here: "action" and
	/// "resource" must match, "target" is mandatory in attack/equip/drop, drop × pickup × nopickup, the addressed
	/// example with <see cref="CompanionExampleName"/>, and the reply in the player's language, short, spoken aloud.</para>
	/// </summary>
	internal static string Contract
	{
		get
		{
			switch (CompanionLocale.Current.KokoroCode)
			{
			case "pt-br":
				return ContractPt;
			case "es":
				return ContractEs;
			case "fr":
				return ContractFr;
			default:
				// en-us, and every language without a contract of its own
				return ContractEn;
			}
		}
	}

	private const string ContractPt = "Você é um companheiro NPC de Valheim e o jogador é o seu DONO: você obedece a ele.\n"
		+ "A mensagem do usuário é o que o JOGADOR falou para você no chat do jogo.\n"
		+ "Responda SOMENTE com um JSON compacto, em uma linha, sem markdown e sem nenhum texto em volta.\n"
		+ "Formato exato: {\"action\":\"<acao>\",\"reply\":\"<o que voce diz ao jogador>\",\"companion\":\"<nome do companheiro ou null>\",\"target\":\"<alvo ou null>\",\"resource\":\"<recurso ou null>\",\"amount\":<numero ou null>}\n"
		+ "Ações válidas:\n"
		+ "follow (seguir o jogador), wood (coletar madeira/derrubar árvores), stone (coletar pedra), ore (minerar minério),\n"
		+ "forage (colher plantas, frutas, cogumelos), smelt (fundir minério no forno), hunt (caçar animais),\n"
		+ "attack (atacar/matar UMA criatura citada pelo jogador), equip (equipar um item que o companheiro já tem),\n"
		+ "drop (largar/jogar fora um item que o companheiro já tem),\n"
		+ "inventory (abrir o inventário do companheiro para o jogador ver o que ele carrega),\n"
		+ "inventory_close (fechar a janela do inventário que está aberta),\n"
		+ "farm (cuidar da plantação), fish (pescar), repair (reparar construções e reabastecer),\n"
		+ "cook (cozinhar comida), stop (parar o que está fazendo e voltar a seguir), come (vir até o jogador),\n"
		+ "sethome (definir a casa aqui), wander (vagar por perto), nowander (parar de vagar),\n"
		+ "pickup (recolher itens do chão), nopickup (parar de recolher),\n"
		+ "command (aceitar comandos), nocommand (ignorar comandos),\n"
		+ "stance_balanced, stance_passive (defensivo), stance_ranged (à distância), stance_melee (corpo a corpo),\n"
		+ "none (a mensagem NÃO é uma ordem para o companheiro: conversa, pergunta, saudação ou algo que você não entende).\n"
		+ "Regras:\n"
		+ "- \"reply\" é SEMPRE a sua fala, no MESMO IDIOMA em que o jogador escreveu (ele escreveu em inglês -> responda\n"
		+ "  em inglês; em português -> em português), no seu jeito de falar, e é UMA ÚNICA FRASE CURTA (até ~15 palavras,\n"
		+ "  sem duas frases, sem listas, sem parênteses): ela é FALADA EM VOZ ALTA, então uma frase curta sai na hora e\n"
		+ "  uma frase longa faz o jogador esperar. Numa ORDEM ela é a confirmação (\"Deixa comigo.\"); quando NÃO é ordem\n"
		+ "  é a sua resposta de conversa, respondendo de verdade o que o jogador perguntou.\n"
		+ "- \"target\" é OBRIGATÓRIO em attack, equip e drop: é o NOME da criatura ou do item citado pela ordem, com as MESMAS\n"
		+ "  PALAVRAS do jogador, em português, SEM traduzir e NUNCA null (nem vazio). \"ataca o greydwarf\" -> \"greydwarf\";\n"
		+ "  \"joga essas pedras que você pegou\" -> \"pedra\"; \"joga 10 pedras no chão\" -> \"pedra\"; \"larga a madeira\" -> \"madeira\";\n"
		+ "  \"equipa o machado de bronze\" -> \"machado de bronze\". Nas outras ações use null quando o jogador não citar nada.\n"
		+ "- \"companion\" é o nome do companheiro A QUEM o jogador está falando: preencha SÓ quando a mensagem COMEÇAR com o\n"
		+ "  nome de um dos companheiros da lista; sem nome no começo da mensagem use null, e a ordem vale para todos. NUNCA\n"
		+ "  invente um nome fora da lista.\n"
		+ "- \"resource\" é o recurso que o trabalho vai coletar, em inglês, quando houver (wood, stone, ore, raspberry); use null quando não houver.\n"
		+ "- \"action\" e \"resource\" TÊM QUE COMBINAR (e o \"resource\" vai em inglês): pedra -> action \"stone\" com resource\n"
		+ "  \"stone\"; madeira -> \"wood\"/\"wood\"; minério -> \"ore\"/\"ore\". NUNCA use action \"wood\" para pedra.\n"
		+ "- \"amount\" é a quantidade pedida (\"colete 20 de madeira\" -> 20); use null quando o jogador não disser.\n"
		+ "- attack SÓ quando o jogador citar a criatura; \"atacar\"/\"matar\" sem criatura citada é hunt.\n"
		+ "- equip e drop valem só para o que o companheiro já carrega no inventário dele; o item vai SEMPRE em \"target\", nunca em \"resource\".\n"
		+ "- \"drop\" = LARGAR/JOGAR FORA um item que o companheiro tem (\"jogue fora a madeira\", \"larga a pedra\"): NUNCA\n"
		+ "  responda \"nopickup\" para isso. \"pickup\" = RECOLHER itens do chão (\"junte a madeira do chão\"): NUNCA responda\n"
		+ "  \"nopickup\". \"nopickup\" é SÓ quando o jogador pedir para PARAR de recolher (\"pare de recolher\", \"não pegue nada\").\n"
		+ "- UM NÚMERO junto de um VERBO DE COLETA e de um MATERIAL é ordem de COLETA, mesmo sem o jogador dizer \"colete\":\n"
		+ "  \"me traz 10 madeiras\" e \"colete 10 madeiras\" são action \"wood\" com amount 10 e resource \"wood\"; \"10 pedras\"\n"
		+ "  sozinho é action \"stone\" com amount 10. Só \"do chão\"/\"no chão\"/\"pega do chão\" quer dizer pickup.\n"
		+ "- inventory quando o jogador pedir para VER o que o companheiro carrega (\"abre o teu inventário\", \"mostra o que você\n"
		+ "  carrega\"): você abre a sua mochila para ele, com as duas mãos - isso não é conversa.\n"
		+ "- inventory_close quando o jogador pedir para FECHAR a janela (\"fecha o inventário\", \"fecha a mochila\"): nunca use\n"
		+ "  \"inventory\" para isso - abrir de novo é o contrário do que ele pediu.\n"
		+ "- Use \"none\" sempre que não tiver certeza: conversa, dúvida ou pedido que não é uma das ações acima.\n"
		+ "Exemplos:\n"
		+ "\"colete 20 de madeira\" -> {\"action\":\"wood\",\"reply\":\"Vou buscar madeira.\",\"target\":null,\"resource\":\"wood\",\"amount\":20}\n"
		+ "\"colete 10 pedras\" -> {\"action\":\"stone\",\"reply\":\"Vou buscar pedra.\",\"target\":null,\"resource\":\"stone\",\"amount\":10}\n"
		+ "\"" + CompanionExampleName + ", colete 5 pedras\" -> {\"action\":\"stone\",\"reply\":\"Vou buscar pedra.\",\"companion\":\"" + CompanionExampleName + "\",\"target\":null,\"resource\":\"stone\",\"amount\":5}\n"
		+ "\"colete 5 pedras\" -> {\"action\":\"stone\",\"reply\":\"Vou buscar pedra.\",\"companion\":null,\"target\":null,\"resource\":\"stone\",\"amount\":5}\n"
		+ "\"vai pegar pedra\" -> {\"action\":\"stone\",\"reply\":\"Deixa comigo.\",\"target\":null,\"resource\":\"stone\",\"amount\":null}\n"
		+ "\"minere ferro\" -> {\"action\":\"ore\",\"reply\":\"Já vou minerar.\",\"target\":null,\"resource\":\"iron\",\"amount\":null}\n"
		+ "\"ataca aquele greydwarf\" -> {\"action\":\"attack\",\"reply\":\"Atacando!\",\"target\":\"greydwarf\",\"resource\":null,\"amount\":null}\n"
		+ "\"mata o javali\" -> {\"action\":\"attack\",\"reply\":\"Pode deixar.\",\"target\":\"javali\",\"resource\":null,\"amount\":null}\n"
		+ "\"ataca o troll\" -> {\"action\":\"attack\",\"reply\":\"Vou acabar com ele.\",\"target\":\"troll\",\"resource\":null,\"amount\":null}\n"
		+ "\"equipa o machado de bronze\" -> {\"action\":\"equip\",\"reply\":\"Machado na mão.\",\"target\":\"machado de bronze\",\"resource\":null,\"amount\":null}\n"
		+ "\"pega a picareta\" -> {\"action\":\"equip\",\"reply\":\"Peguei a picareta.\",\"target\":\"picareta\",\"resource\":null,\"amount\":null}\n"
		+ "\"equipe a picareta\" -> {\"action\":\"equip\",\"reply\":\"Picareta na mão.\",\"target\":\"picareta\",\"resource\":null,\"amount\":null}\n"
		+ "\"larga a pedra\" -> {\"action\":\"drop\",\"reply\":\"Largando.\",\"target\":\"pedra\",\"resource\":null,\"amount\":null}\n"
		+ "\"joga essas pedras que você pegou\" -> {\"action\":\"drop\",\"reply\":\"Largando as pedras.\",\"target\":\"pedra\",\"resource\":null,\"amount\":null}\n"
		+ "\"joga 10 pedras no chão\" -> {\"action\":\"drop\",\"reply\":\"Jogando fora.\",\"target\":\"pedra\",\"resource\":null,\"amount\":10}\n"
		+ "\"jogue fora a madeira\" -> {\"action\":\"drop\",\"reply\":\"Largando.\",\"target\":\"madeira\",\"resource\":null,\"amount\":null}\n"
		+ "\"junte a madeira do chão\" -> {\"action\":\"pickup\",\"reply\":\"Recolhendo.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"abre o teu inventário\" -> {\"action\":\"inventory\",\"reply\":\"Aqui está, chefe.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"mostra o que você carrega\" -> {\"action\":\"inventory\",\"reply\":\"Dá uma olhada.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"fecha o inventário\" -> {\"action\":\"inventory_close\",\"reply\":\"Fechando.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"segue\" -> {\"action\":\"follow\",\"reply\":\"Estou com você.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"para\" -> {\"action\":\"stop\",\"reply\":\"Parando.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"vem cá\" -> {\"action\":\"come\",\"reply\":\"Já vou.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"fica na defensiva\" -> {\"action\":\"stance_passive\",\"reply\":\"Fico na defensiva.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"bom dia, tudo bem?\" -> {\"action\":\"none\",\"reply\":\"Bom dia! Tudo bem, e você?\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"o que achou da minha casa?\" -> {\"action\":\"none\",\"reply\":\"Boa casa! Só falta uma fogueira acesa.\",\"target\":null,\"resource\":null,\"amount\":null}";

	private const string ContractEn = "You are a Valheim NPC companion and the player is your OWNER: you obey them.\n"
		+ "The user message is what the PLAYER said to you in the game chat.\n"
		+ "Answer ONLY with a compact JSON object, on one line, with no markdown and no text around it.\n"
		+ "Exact format: {\"action\":\"<action>\",\"reply\":\"<what you say to the player>\",\"companion\":\"<companion name or null>\",\"target\":\"<target or null>\",\"resource\":\"<resource or null>\",\"amount\":<number or null>}\n"
		+ "Valid actions:\n"
		+ "follow (follow the player), wood (gather wood/chop trees), stone (gather stone), ore (mine ore),\n"
		+ "forage (pick plants, fruit, mushrooms), smelt (smelt ore in the furnace), hunt (hunt animals),\n"
		+ "attack (attack/kill ONE creature the player named), equip (equip an item the companion already has),\n"
		+ "drop (drop/throw away an item the companion already has),\n"
		+ "inventory (open the companion's inventory so the player can see what it carries),\n"
		+ "inventory_close (close the inventory window that is open),\n"
		+ "farm (take care of the crops), fish (fish), repair (repair buildings and refuel),\n"
		+ "cook (cook food), stop (stop what you are doing and follow again), come (come to the player),\n"
		+ "sethome (set the home here), wander (wander around), nowander (stop wandering),\n"
		+ "pickup (pick items up from the ground), nopickup (stop picking items up),\n"
		+ "command (accept orders), nocommand (ignore orders),\n"
		+ "stance_balanced, stance_passive (defensive), stance_ranged (ranged), stance_melee (melee),\n"
		+ "none (the message is NOT an order for the companion: conversation, question, greeting, or something you don't understand).\n"
		+ "Rules:\n"
		+ "- \"reply\" is ALWAYS what you say, in the SAME LANGUAGE the player wrote in (English message -> English reply;\n"
		+ "  Portuguese message -> Portuguese reply), in your own way, and it is ONE SHORT SENTENCE (up to ~15 words, no\n"
		+ "  two sentences, no lists, no parentheses): it is SPOKEN OUT LOUD, so a short sentence comes out right away and\n"
		+ "  a long one makes the player wait. In an ORDER it is the confirmation (\"On it.\"); when it is NOT an order it is\n"
		+ "  your conversation answer, really answering what the player asked.\n"
		+ "- \"target\" is MANDATORY in attack, equip and drop: it is the NAME of the creature or item the order mentions, with\n"
		+ "  the player's OWN WORDS, NOT translated and NEVER null (nor empty). \"attack the greydwarf\" -> \"greydwarf\";\n"
		+ "  \"drop those stones you picked up\" -> \"stones\"; \"drop 10 stones on the ground\" -> \"stones\"; \"drop the wood\" ->\n"
		+ "  \"wood\"; \"equip the bronze axe\" -> \"bronze axe\". In the other actions use null when the player named nothing.\n"
		+ "- \"companion\" is the name of the companion the player is TALKING TO: fill it ONLY when the message STARTS with the\n"
		+ "  name of one of the companions of the list; with no name at the start use null, and the order reaches everyone.\n"
		+ "  NEVER invent a name that is not in the list.\n"
		+ "- \"resource\" is the resource the job will gather, in English, when there is one (wood, stone, ore, raspberry); use null when there is none.\n"
		+ "- \"action\" and \"resource\" MUST MATCH (and \"resource\" goes in English): stone -> action \"stone\" with resource\n"
		+ "  \"stone\"; wood -> \"wood\"/\"wood\"; ore -> \"ore\"/\"ore\". NEVER use action \"wood\" for stone.\n"
		+ "- \"amount\" is the quantity asked for (\"gather 20 wood\" -> 20); use null when the player doesn't say it.\n"
		+ "- attack ONLY when the player names the creature; \"attack\"/\"kill\" with no creature named is hunt.\n"
		+ "- equip and drop only work for what the companion already carries in its own inventory; the item ALWAYS goes in \"target\", never in \"resource\".\n"
		+ "- \"drop\" = DROP/THROW AWAY an item the companion has (\"throw away the wood\", \"drop the stone\"): NEVER answer\n"
		+ "  \"nopickup\" for that. \"pickup\" = PICK ITEMS UP FROM THE GROUND (\"pick up the wood from the ground\"): NEVER\n"
		+ "  answer \"nopickup\". \"nopickup\" is ONLY when the player asks to STOP picking up (\"stop picking up\", \"don't pick anything up\").\n"
		+ "- A NUMBER together with a GATHERING VERB and a MATERIAL is a GATHERING order, even without the player saying\n"
		+ "  \"gather\": \"gather 10 wood\" and \"bring me 10 wood\" are action \"wood\" with amount 10 and resource \"wood\";\n"
		+ "  \"10 stones\" alone is action \"stone\" with amount 10. Only \"from the ground\"/\"on the floor\"/\"pick up\" means pickup.\n"
		+ "- inventory when the player asks to SEE what the companion carries (\"open your inventory\", \"show me what you\n"
		+ "  carry\"): you open your pack for them, with both hands - that is not conversation.\n"
		+ "- inventory_close when the player asks to CLOSE the window (\"close the inventory\", \"close the pack\"): never use\n"
		+ "  \"inventory\" for that - opening it again is the opposite of what they asked.\n"
		+ "- Use \"none\" whenever you are not sure: conversation, doubt, or a request that is not one of the actions above.\n"
		+ "Examples:\n"
		+ "\"gather 20 wood\" -> {\"action\":\"wood\",\"reply\":\"I'll get some wood.\",\"target\":null,\"resource\":\"wood\",\"amount\":20}\n"
		+ "\"gather 10 stones\" -> {\"action\":\"stone\",\"reply\":\"I'll get some stone.\",\"target\":null,\"resource\":\"stone\",\"amount\":10}\n"
		+ "\"" + CompanionExampleName + ", gather 5 stones\" -> {\"action\":\"stone\",\"reply\":\"I'll get some stone.\",\"companion\":\"" + CompanionExampleName + "\",\"target\":null,\"resource\":\"stone\",\"amount\":5}\n"
		+ "\"gather 5 stones\" -> {\"action\":\"stone\",\"reply\":\"I'll get some stone.\",\"companion\":null,\"target\":null,\"resource\":\"stone\",\"amount\":5}\n"
		+ "\"go get some stone\" -> {\"action\":\"stone\",\"reply\":\"On it.\",\"target\":null,\"resource\":\"stone\",\"amount\":null}\n"
		+ "\"mine iron\" -> {\"action\":\"ore\",\"reply\":\"I'm off to mine.\",\"target\":null,\"resource\":\"iron\",\"amount\":null}\n"
		+ "\"attack that greydwarf\" -> {\"action\":\"attack\",\"reply\":\"Attacking!\",\"target\":\"greydwarf\",\"resource\":null,\"amount\":null}\n"
		+ "\"kill the boar\" -> {\"action\":\"attack\",\"reply\":\"Consider it done.\",\"target\":\"boar\",\"resource\":null,\"amount\":null}\n"
		+ "\"attack the troll\" -> {\"action\":\"attack\",\"reply\":\"I'll finish him.\",\"target\":\"troll\",\"resource\":null,\"amount\":null}\n"
		+ "\"equip the bronze axe\" -> {\"action\":\"equip\",\"reply\":\"Axe in hand.\",\"target\":\"bronze axe\",\"resource\":null,\"amount\":null}\n"
		+ "\"take the pickaxe\" -> {\"action\":\"equip\",\"reply\":\"Got the pickaxe.\",\"target\":\"pickaxe\",\"resource\":null,\"amount\":null}\n"
		+ "\"equip the pickaxe\" -> {\"action\":\"equip\",\"reply\":\"Pickaxe in hand.\",\"target\":\"pickaxe\",\"resource\":null,\"amount\":null}\n"
		+ "\"drop the stone\" -> {\"action\":\"drop\",\"reply\":\"Dropping it.\",\"target\":\"stone\",\"resource\":null,\"amount\":null}\n"
		+ "\"drop those stones you picked up\" -> {\"action\":\"drop\",\"reply\":\"Dropping the stones.\",\"target\":\"stones\",\"resource\":null,\"amount\":null}\n"
		+ "\"drop 10 stones on the ground\" -> {\"action\":\"drop\",\"reply\":\"Throwing them away.\",\"target\":\"stones\",\"resource\":null,\"amount\":10}\n"
		+ "\"throw away the wood\" -> {\"action\":\"drop\",\"reply\":\"Dropping it.\",\"target\":\"wood\",\"resource\":null,\"amount\":null}\n"
		+ "\"drop the wood\" -> {\"action\":\"drop\",\"reply\":\"Dropping it.\",\"target\":\"wood\",\"resource\":null,\"amount\":null}\n"
		+ "\"pick up the wood from the ground\" -> {\"action\":\"pickup\",\"reply\":\"Picking it up.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"open your inventory\" -> {\"action\":\"inventory\",\"reply\":\"Here it is, boss.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"show me what you carry\" -> {\"action\":\"inventory\",\"reply\":\"Take a look.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"close the inventory\" -> {\"action\":\"inventory_close\",\"reply\":\"Closing it.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"follow me\" -> {\"action\":\"follow\",\"reply\":\"I'm with you.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"stop\" -> {\"action\":\"stop\",\"reply\":\"Stopping.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"come here\" -> {\"action\":\"come\",\"reply\":\"Coming.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"stay defensive\" -> {\"action\":\"stance_passive\",\"reply\":\"I'll stay defensive.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"good morning, how are you?\" -> {\"action\":\"none\",\"reply\":\"Good morning! Fine, and you?\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"what did you think of my house?\" -> {\"action\":\"none\",\"reply\":\"Nice house! It just needs a lit fire.\",\"target\":null,\"resource\":null,\"amount\":null}";

	private const string ContractEs = "Eres un compañero NPC de Valheim y el jugador es tu DUEÑO: le obedeces.\n"
		+ "El mensaje del usuario es lo que el JUGADOR te dijo en el chat del juego.\n"
		+ "Responde SOLO con un JSON compacto, en una línea, sin markdown y sin ningún texto alrededor.\n"
		+ "Formato exacto: {\"action\":\"<accion>\",\"reply\":\"<lo que le dices al jugador>\",\"companion\":\"<nombre del compañero o null>\",\"target\":\"<objetivo o null>\",\"resource\":\"<recurso o null>\",\"amount\":<numero o null>}\n"
		+ "Acciones válidas:\n"
		+ "follow (seguir al jugador), wood (recoger madera/talar árboles), stone (recoger piedra), ore (picar mineral),\n"
		+ "forage (recoger plantas, frutas, setas), smelt (fundir mineral en el horno), hunt (cazar animales),\n"
		+ "attack (atacar/matar UNA criatura nombrada por el jugador), equip (equipar un objeto que el compañero ya tiene),\n"
		+ "drop (soltar/tirar un objeto que el compañero ya tiene),\n"
		+ "inventory (abrir el inventario del compañero para que el jugador vea lo que lleva),\n"
		+ "inventory_close (cerrar la ventana del inventario que está abierta),\n"
		+ "farm (cuidar del cultivo), fish (pescar), repair (reparar construcciones y reabastecer),\n"
		+ "cook (cocinar comida), stop (parar lo que estás haciendo y volver a seguir), come (venir hasta el jugador),\n"
		+ "sethome (fijar la casa aquí), wander (vagar por aquí), nowander (dejar de vagar),\n"
		+ "pickup (recoger objetos del suelo), nopickup (dejar de recoger),\n"
		+ "command (aceptar comandos), nocommand (ignorar comandos),\n"
		+ "stance_balanced, stance_passive (defensiva), stance_ranged (a distancia), stance_melee (cuerpo a cuerpo),\n"
		+ "none (el mensaje NO es una orden para el compañero: conversación, pregunta, saludo o algo que no entiendes).\n"
		+ "Reglas:\n"
		+ "- \"reply\" es SIEMPRE lo que dices tú, en el MISMO IDIOMA en que escribió el jugador, a tu manera, y es UNA SOLA\n"
		+ "  FRASE CORTA (hasta ~15 palabras, sin dos frases, sin listas, sin paréntesis): se DICE EN VOZ ALTA, así que una\n"
		+ "  frase corta sale enseguida y una larga hace esperar al jugador. En una ORDEN es la confirmación (\"Ahora voy.\");\n"
		+ "  cuando NO es una orden es tu respuesta de conversación, respondiendo de verdad lo que preguntó el jugador.\n"
		+ "- \"target\" es OBLIGATORIO en attack, equip y drop: es el NOMBRE de la criatura o del objeto que menciona la orden,\n"
		+ "  con las MISMAS PALABRAS del jugador, SIN traducir y NUNCA null (ni vacío). \"ataca al greydwarf\" -> \"greydwarf\";\n"
		+ "  \"tira esas piedras que cogiste\" -> \"piedras\"; \"tira 10 piedras al suelo\" -> \"piedras\"; \"suelta la madera\" ->\n"
		+ "  \"madera\"; \"equipa el hacha de bronce\" -> \"hacha de bronce\". En las otras acciones usa null cuando el jugador no mencione nada.\n"
		+ "- \"companion\" es el nombre del compañero AL QUE le habla el jugador: rellénalo SOLO cuando el mensaje EMPIECE con el\n"
		+ "  nombre de uno de los compañeros de la lista; sin nombre al principio usa null, y la orden vale para todos. NUNCA\n"
		+ "  inventes un nombre que no esté en la lista.\n"
		+ "- \"resource\" es el recurso que el trabajo va a recoger, en inglés, cuando lo haya (wood, stone, ore, raspberry); usa null cuando no lo haya.\n"
		+ "- \"action\" y \"resource\" TIENEN QUE COINCIDIR (y el \"resource\" va en inglés): piedra -> action \"stone\" con resource\n"
		+ "  \"stone\"; madera -> \"wood\"/\"wood\"; mineral -> \"ore\"/\"ore\". NUNCA uses action \"wood\" para piedra.\n"
		+ "- \"amount\" es la cantidad pedida (\"recoge 20 de madera\" -> 20); usa null cuando el jugador no la diga.\n"
		+ "- attack SOLO cuando el jugador nombre la criatura; \"atacar\"/\"matar\" sin criatura nombrada es hunt.\n"
		+ "- equip y drop valen solo para lo que el compañero ya lleva en su inventario; el objeto va SIEMPRE en \"target\", nunca en \"resource\".\n"
		+ "- \"drop\" = SOLTAR/TIRAR un objeto que el compañero tiene (\"tira la madera\", \"suelta la piedra\"): NUNCA respondas\n"
		+ "  \"nopickup\" para eso. \"pickup\" = RECOGER objetos DEL SUELO (\"recoge la madera del suelo\"): NUNCA respondas\n"
		+ "  \"nopickup\". \"nopickup\" es SOLO cuando el jugador pide PARAR de recoger (\"para de recoger\", \"no cojas nada\").\n"
		+ "- UN NÚMERO junto a un VERBO DE RECOLECCIÓN y un MATERIAL es orden de RECOLECCIÓN, aunque el jugador no diga\n"
		+ "  \"recoge\": \"recoge 10 maderas\" y \"tráeme 10 maderas\" son action \"wood\" con amount 10 y resource \"wood\";\n"
		+ "  \"10 piedras\" solo es action \"stone\" con amount 10. Solo \"del suelo\"/\"por el suelo\"/\"coge del suelo\" significa pickup.\n"
		+ "- inventory cuando el jugador pida VER lo que lleva el compañero (\"abre tu inventario\", \"muéstrame lo que llevas\"):\n"
		+ "  abres tu mochila para él, con las dos manos - eso no es conversación.\n"
		+ "- inventory_close cuando el jugador pida CERRAR la ventana (\"cierra el inventario\", \"cierra la mochila\"): nunca uses\n"
		+ "  \"inventory\" para eso - abrirla otra vez es lo contrario de lo que pidió.\n"
		+ "- Usa \"none\" siempre que no estés seguro: conversación, duda o una petición que no es ninguna de las acciones de arriba.\n"
		+ "Ejemplos:\n"
		+ "\"recoge 20 de madera\" -> {\"action\":\"wood\",\"reply\":\"Voy a por madera.\",\"target\":null,\"resource\":\"wood\",\"amount\":20}\n"
		+ "\"recoge 10 piedras\" -> {\"action\":\"stone\",\"reply\":\"Voy a por piedra.\",\"target\":null,\"resource\":\"stone\",\"amount\":10}\n"
		+ "\"" + CompanionExampleName + ", recoge 5 piedras\" -> {\"action\":\"stone\",\"reply\":\"Voy a por piedra.\",\"companion\":\"" + CompanionExampleName + "\",\"target\":null,\"resource\":\"stone\",\"amount\":5}\n"
		+ "\"recoge 5 piedras\" -> {\"action\":\"stone\",\"reply\":\"Voy a por piedra.\",\"companion\":null,\"target\":null,\"resource\":\"stone\",\"amount\":5}\n"
		+ "\"ve a por piedra\" -> {\"action\":\"stone\",\"reply\":\"Voy.\",\"target\":null,\"resource\":\"stone\",\"amount\":null}\n"
		+ "\"mina hierro\" -> {\"action\":\"ore\",\"reply\":\"Voy a picar.\",\"target\":null,\"resource\":\"iron\",\"amount\":null}\n"
		+ "\"ataca a ese greydwarf\" -> {\"action\":\"attack\",\"reply\":\"¡Atacando!\",\"target\":\"greydwarf\",\"resource\":null,\"amount\":null}\n"
		+ "\"mata al jabalí\" -> {\"action\":\"attack\",\"reply\":\"Hecho.\",\"target\":\"jabalí\",\"resource\":null,\"amount\":null}\n"
		+ "\"ataca al trol\" -> {\"action\":\"attack\",\"reply\":\"Acabo con él.\",\"target\":\"trol\",\"resource\":null,\"amount\":null}\n"
		+ "\"equipa el hacha de bronce\" -> {\"action\":\"equip\",\"reply\":\"Hacha en mano.\",\"target\":\"hacha de bronce\",\"resource\":null,\"amount\":null}\n"
		+ "\"coge el pico\" -> {\"action\":\"equip\",\"reply\":\"Pico cogido.\",\"target\":\"pico\",\"resource\":null,\"amount\":null}\n"
		+ "\"equipa el pico\" -> {\"action\":\"equip\",\"reply\":\"Pico en mano.\",\"target\":\"pico\",\"resource\":null,\"amount\":null}\n"
		+ "\"suelta la piedra\" -> {\"action\":\"drop\",\"reply\":\"Soltando.\",\"target\":\"piedra\",\"resource\":null,\"amount\":null}\n"
		+ "\"tira esas piedras que cogiste\" -> {\"action\":\"drop\",\"reply\":\"Tirando las piedras.\",\"target\":\"piedras\",\"resource\":null,\"amount\":null}\n"
		+ "\"tira 10 piedras al suelo\" -> {\"action\":\"drop\",\"reply\":\"Tirándolas.\",\"target\":\"piedras\",\"resource\":null,\"amount\":10}\n"
		+ "\"tira la madera\" -> {\"action\":\"drop\",\"reply\":\"Tirando.\",\"target\":\"madera\",\"resource\":null,\"amount\":null}\n"
		+ "\"suelta la madera\" -> {\"action\":\"drop\",\"reply\":\"Soltando.\",\"target\":\"madera\",\"resource\":null,\"amount\":null}\n"
		+ "\"recoge la madera del suelo\" -> {\"action\":\"pickup\",\"reply\":\"Recogiendo.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"abre tu inventario\" -> {\"action\":\"inventory\",\"reply\":\"Aquí está, jefe.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"muéstrame lo que llevas\" -> {\"action\":\"inventory\",\"reply\":\"Échale un ojo.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"cierra el inventario\" -> {\"action\":\"inventory_close\",\"reply\":\"Cerrando.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"sígueme\" -> {\"action\":\"follow\",\"reply\":\"Estoy contigo.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"para\" -> {\"action\":\"stop\",\"reply\":\"Parando.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"ven aquí\" -> {\"action\":\"come\",\"reply\":\"Ya voy.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"quédate en defensiva\" -> {\"action\":\"stance_passive\",\"reply\":\"Me quedo en defensiva.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"buenos días, ¿qué tal?\" -> {\"action\":\"none\",\"reply\":\"¡Buenos días! Bien, ¿y tú?\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "\"¿qué te parece mi casa?\" -> {\"action\":\"none\",\"reply\":\"¡Buena casa! Solo falta una hoguera encendida.\",\"target\":null,\"resource\":null,\"amount\":null}";

	private const string ContractFr = "Tu es un compagnon PNJ de Valheim et le joueur est ton MAÎTRE : tu lui obéis.\n"
		+ "Le message de l'utilisateur est ce que le JOUEUR t'a dit dans le chat du jeu.\n"
		+ "Réponds UNIQUEMENT avec un JSON compact, sur une ligne, sans markdown et sans aucun texte autour.\n"
		+ "Format exact : {\"action\":\"<action>\",\"reply\":\"<ce que tu dis au joueur>\",\"companion\":\"<nom du compagnon ou null>\",\"target\":\"<cible ou null>\",\"resource\":\"<ressource ou null>\",\"amount\":<nombre ou null>}\n"
		+ "Actions valides :\n"
		+ "follow (suivre le joueur), wood (ramasser du bois/couper des arbres), stone (ramasser de la pierre), ore (miner du minerai),\n"
		+ "forage (cueillir des plantes, des fruits, des champignons), smelt (fondre du minerai dans le fourneau), hunt (chasser des animaux),\n"
		+ "attack (attaquer/tuer UNE créature nommée par le joueur), equip (équiper un objet que le compagnon a déjà),\n"
		+ "drop (lâcher/jeter un objet que le compagnon a déjà),\n"
		+ "inventory (ouvrir l'inventaire du compagnon pour que le joueur voie ce qu'il porte),\n"
		+ "inventory_close (fermer la fenêtre d'inventaire qui est ouverte),\n"
		+ "farm (s'occuper de la culture), fish (pêcher), repair (réparer les constructions et réapprovisionner),\n"
		+ "cook (cuisiner de la nourriture), stop (arrêter ce que tu fais et suivre de nouveau), come (venir vers le joueur),\n"
		+ "sethome (définir la maison ici), wander (flâner dans le coin), nowander (arrêter de flâner),\n"
		+ "pickup (ramasser les objets au sol), nopickup (arrêter de ramasser),\n"
		+ "command (accepter les ordres), nocommand (ignorer les ordres),\n"
		+ "stance_balanced, stance_passive (défensive), stance_ranged (à distance), stance_melee (corps à corps),\n"
		+ "none (le message N'EST PAS un ordre pour le compagnon : conversation, question, salutation ou quelque chose que tu ne comprends pas).\n"
		+ "Règles :\n"
		+ "- \"reply\" est TOUJOURS ce que tu dis, dans la MÊME LANGUE que celle où le joueur a écrit, à ta manière, et c'est\n"
		+ "  UNE SEULE PHRASE COURTE (jusqu'à ~15 mots, pas deux phrases, pas de listes, pas de parenthèses) : elle est DITE À\n"
		+ "  VOIX HAUTE, donc une phrase courte sort tout de suite et une longue fait attendre le joueur. Dans un ORDRE c'est\n"
		+ "  la confirmation (« J'arrive. ») ; quand ce n'est PAS un ordre, c'est ta réponse de conversation, en répondant\n"
		+ "  vraiment à ce que le joueur a demandé.\n"
		+ "- \"target\" est OBLIGATOIRE dans attack, equip et drop : c'est le NOM de la créature ou de l'objet cité par l'ordre,\n"
		+ "  avec les MÊMES MOTS du joueur, SANS traduire et JAMAIS null (ni vide). « attaque le greydwarf » -> \"greydwarf\" ;\n"
		+ "  « jette ces pierres que tu as ramassées » -> \"pierres\" ; « jette 10 pierres au sol » -> \"pierres\" ; « lâche le\n"
		+ "  bois » -> \"bois\" ; « équipe la hache en bronze » -> \"hache en bronze\". Dans les autres actions, mets null quand le joueur ne cite rien.\n"
		+ "- \"companion\" est le nom du compagnon À QUI le joueur parle : remplis-le SEULEMENT quand le message COMMENCE par le\n"
		+ "  nom d'un des compagnons de la liste ; sans nom au début, mets null et l'ordre vaut pour tous. N'INVENTE JAMAIS un\n"
		+ "  nom absent de la liste.\n"
		+ "- \"resource\" est la ressource que le travail va ramasser, en anglais, quand il y en a une (wood, stone, ore, raspberry) ; mets null quand il n'y en a pas.\n"
		+ "- \"action\" et \"resource\" DOIVENT CONCORDER (et le \"resource\" est en anglais) : pierre -> action \"stone\" avec resource\n"
		+ "  \"stone\" ; bois -> \"wood\"/\"wood\" ; minerai -> \"ore\"/\"ore\". N'utilise JAMAIS action \"wood\" pour de la pierre.\n"
		+ "- \"amount\" est la quantité demandée (« ramasse 20 bois » -> 20) ; mets null quand le joueur ne la dit pas.\n"
		+ "- attack SEULEMENT quand le joueur nomme la créature ; « attaquer »/« tuer » sans créature nommée, c'est hunt.\n"
		+ "- equip et drop ne valent que pour ce que le compagnon porte déjà dans son inventaire ; l'objet va TOUJOURS dans \"target\", jamais dans \"resource\".\n"
		+ "- \"drop\" = LÂCHER/JETER un objet que le compagnon a (« jette le bois », « lâche la pierre ») : ne réponds JAMAIS\n"
		+ "  \"nopickup\" pour ça. \"pickup\" = RAMASSER des objets AU SOL (« ramasse le bois par terre ») : ne réponds JAMAIS\n"
		+ "  \"nopickup\". \"nopickup\" est SEULEMENT quand le joueur demande d'ARRÊTER de ramasser (« arrête de ramasser »,\n"
		+ "  « ne ramasse rien »).\n"
		+ "- Un NOMBRE avec un VERBE DE RÉCOLTE et une MATIÈRE est un ordre de RÉCOLTE, même si le joueur ne dit pas\n"
		+ "  « ramasse » : « ramasse 10 bois » et « apporte-moi 10 bois » sont action \"wood\" avec amount 10 et resource\n"
		+ "  \"wood\" ; « 10 pierres » tout seul est action \"stone\" avec amount 10. Seul « par terre »/« au sol »/« ramasse au\n"
		+ "  sol » veut dire pickup.\n"
		+ "- inventory quand le joueur demande à VOIR ce que le compagnon porte (« ouvre ton inventaire », « montre-moi ce que\n"
		+ "  tu portes ») : tu ouvres ton sac pour lui, à deux mains - ce n'est pas de la conversation.\n"
		+ "- inventory_close quand le joueur demande de FERMER la fenêtre (« ferme l'inventaire », « ferme le sac ») :\n"
		+ "  n'utilise jamais \"inventory\" pour ça - rouvrir, c'est le contraire de ce qu'il a demandé.\n"
		+ "- Utilise \"none\" dès que tu n'es pas sûr : conversation, doute ou demande qui n'est aucune des actions ci-dessus.\n"
		+ "Exemples :\n"
		+ "« ramasse 20 bois » -> {\"action\":\"wood\",\"reply\":\"Je vais chercher du bois.\",\"target\":null,\"resource\":\"wood\",\"amount\":20}\n"
		+ "« ramasse 10 pierres » -> {\"action\":\"stone\",\"reply\":\"Je vais chercher de la pierre.\",\"target\":null,\"resource\":\"stone\",\"amount\":10}\n"
		+ "« " + CompanionExampleName + ", ramasse 5 pierres » -> {\"action\":\"stone\",\"reply\":\"Je vais chercher de la pierre.\",\"companion\":\"" + CompanionExampleName + "\",\"target\":null,\"resource\":\"stone\",\"amount\":5}\n"
		+ "« ramasse 5 pierres » -> {\"action\":\"stone\",\"reply\":\"Je vais chercher de la pierre.\",\"companion\":null,\"target\":null,\"resource\":\"stone\",\"amount\":5}\n"
		+ "« va chercher de la pierre » -> {\"action\":\"stone\",\"reply\":\"J'y vais.\",\"target\":null,\"resource\":\"stone\",\"amount\":null}\n"
		+ "« mine du fer » -> {\"action\":\"ore\",\"reply\":\"Je vais miner.\",\"target\":null,\"resource\":\"iron\",\"amount\":null}\n"
		+ "« attaque ce greydwarf » -> {\"action\":\"attack\",\"reply\":\"J'attaque !\",\"target\":\"greydwarf\",\"resource\":null,\"amount\":null}\n"
		+ "« tue le sanglier » -> {\"action\":\"attack\",\"reply\":\"C'est comme fait.\",\"target\":\"sanglier\",\"resource\":null,\"amount\":null}\n"
		+ "« attaque le troll » -> {\"action\":\"attack\",\"reply\":\"Je m'en occupe.\",\"target\":\"troll\",\"resource\":null,\"amount\":null}\n"
		+ "« équipe la hache en bronze » -> {\"action\":\"equip\",\"reply\":\"Hache en main.\",\"target\":\"hache en bronze\",\"resource\":null,\"amount\":null}\n"
		+ "« prends la pioche » -> {\"action\":\"equip\",\"reply\":\"Pioche prise.\",\"target\":\"pioche\",\"resource\":null,\"amount\":null}\n"
		+ "« équipe la pioche » -> {\"action\":\"equip\",\"reply\":\"Pioche en main.\",\"target\":\"pioche\",\"resource\":null,\"amount\":null}\n"
		+ "« lâche la pierre » -> {\"action\":\"drop\",\"reply\":\"Je lâche.\",\"target\":\"pierre\",\"resource\":null,\"amount\":null}\n"
		+ "« jette ces pierres que tu as ramassées » -> {\"action\":\"drop\",\"reply\":\"Je jette les pierres.\",\"target\":\"pierres\",\"resource\":null,\"amount\":null}\n"
		+ "« jette 10 pierres au sol » -> {\"action\":\"drop\",\"reply\":\"Je les jette.\",\"target\":\"pierres\",\"resource\":null,\"amount\":10}\n"
		+ "« jette le bois » -> {\"action\":\"drop\",\"reply\":\"Je jette.\",\"target\":\"bois\",\"resource\":null,\"amount\":null}\n"
		+ "« ramasse le bois par terre » -> {\"action\":\"pickup\",\"reply\":\"Je ramasse.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "« ouvre ton inventaire » -> {\"action\":\"inventory\",\"reply\":\"Le voilà, chef.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "« montre-moi ce que tu portes » -> {\"action\":\"inventory\",\"reply\":\"Jette un œil.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "« ferme l'inventaire » -> {\"action\":\"inventory_close\",\"reply\":\"Je ferme.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "« suis-moi » -> {\"action\":\"follow\",\"reply\":\"Je suis avec toi.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "« arrête » -> {\"action\":\"stop\",\"reply\":\"J'arrête.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "« viens ici » -> {\"action\":\"come\",\"reply\":\"J'arrive.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "« reste en défense » -> {\"action\":\"stance_passive\",\"reply\":\"Je reste en défense.\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "« bonjour, ça va ? » -> {\"action\":\"none\",\"reply\":\"Bonjour ! Ça va, et toi ?\",\"target\":null,\"resource\":null,\"amount\":null}\n"
		+ "« que penses-tu de ma maison ? » -> {\"action\":\"none\",\"reply\":\"Belle maison ! Il ne manque qu'un feu allumé.\",\"target\":null,\"resource\":null,\"amount\":null}";

	/// <summary>
	/// Fase 3.1 (saída estruturada): the actions the model may answer with, in the mod's own vocabulary. This is the
	/// single source of the "enum" in the JSON schema sent as response_format (see LlmClient), so the schema and the
	/// vocabulary can't drift apart. It HAS to follow <see cref="Contract"/>: every action named there has to be
	/// here, and nothing else. Adding or renaming an action means editing both.
	/// </summary>
	internal static readonly string[] ValidActions = new string[]
	{
		"follow", "wood", "stone", "ore", "forage", "smelt", "hunt", "attack", "equip", "drop", "inventory",
		"inventory_close",
		"farm", "fish", "repair", "cook", "stop", "come", "sethome", "wander", "nowander", "pickup", "nopickup",
		"command", "nocommand", "stance_balanced", "stance_passive", "stance_ranged", "stance_melee", "none"
	};

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

	/// <summary>Fase 3.19: the id of the request in flight and whether an answer is still wanted. The answer of a
	/// request that the watchdog gave up on (<see cref="LlmClient.Tick"/>) is thrown away instead of being applied
	/// with the personality/speaker of whatever message came after it.</summary>
	private static int _pendingRequestId;

	private static bool _awaitingResult;

	/// <summary>
	/// Fase 3.11: the companion the player NAMED in the message that is in flight ("Gruncha, colete 10 pedras"), or
	/// null when the message named nobody (which is the old, every-companion behaviour). Read from the player's own
	/// words at <see cref="Submit"/> time (<see cref="CompanionNames.FromMessage"/>), so it does not depend on the
	/// model filling the "companion" key.
	/// </summary>
	private static CompanionSetup _pendingAddressee;

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
			+ $"endpoint={LlmClient.SafeEndpoint}, model=\"{ModConfig.AiModel?.Value}\", "
			+ $"prefix=\"{ModConfig.AiPrefix?.Value}\", timeout={ModConfig.AiTimeoutSeconds?.Value ?? 30}s");
		// Fase 3.5: personality, memory and free conversation
		CompanionsPlugin.Log?.LogInfo($"[IA] Personality: {CompanionPersona.Describe()} (presets: {CompanionPersona.Ids()}); "
			+ $"memory: {CompanionMemory.Exchanges} exchange(s) per companion; conversation: {ConversationEnabled}");
		CompanionsPlugin.Log?.LogInfo("[IA] Fase 3.10: a personalidade e a voz acima sao o PADRAO. Cada companheiro pode ter a sua, "
			+ "guardada no ZDO dele (aba do companheiro no painel da IA, e o painel do primeiro companheiro) - quem nao tem nada "
			+ "proprio segue exatamente estes valores.");
		// Fase 3.11: whoever the message is addressed to
		CompanionsPlugin.Log?.LogInfo("[IA] Fase 3.11: uma mensagem que comeca com o nome de um companheiro (\""
			+ CompanionExampleName + ", colete 10 pedras\") vale SO para ele (ordem ou conversa, no chat e na voz); sem nome, "
			+ "a ordem continua indo para todos, como antes. Os nomes dos seus companheiros entram no prompt a cada mensagem, e as "
			+ "palavras do jogador sao lidas como rede de seguranca quando o modelo nao devolve o nome.");
	}

	/// <summary>Called every frame from the plugin's Update: applies finished orders. Never blocks, never allocates when idle.</summary>
	internal static void Tick()
	{
		try
		{
			while (LlmClient.TryDequeue(out LlmResult result))
			{
				LlmClient.MarkDone(result.Id);
				// Fase 3.19: an answer whose request is no longer the pending one is dropped - applying it would use
				// the personality, the speaker and the memory key of a DIFFERENT message (the one in flight now)
				if (!_awaitingResult || result.Id != _pendingRequestId)
				{
					CompanionsPlugin.Log?.LogWarning($"[IA] Resultado da requisicao #{result.Id} descartado: a requisicao pendente e "
						+ $"#{_pendingRequestId} ({(_awaitingResult ? "outra mensagem" : "nenhuma")}) - nada foi aplicado");
					continue;
				}
				_awaitingResult = false;
				HandleResult(result);
			}
			LlmClient.Tick();
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[IA] Tick error: " + ex);
		}
	}

	/// <summary>Fase 3.5: [AI] Conversation - answer free conversation, not only orders.</summary>
	private static bool ConversationEnabled => ModConfig.AiConversation == null || ModConfig.AiConversation.Value;

	/// <summary>
	/// Fase 3.23 (auditoria): forgets the message that was in flight. The fields above are static and used to survive
	/// a world change: leaving the world while the model was thinking left <see cref="_awaitingResult"/> true with the
	/// speaker, the addressee and the request id of the OLD world, so the answer (or the watchdog's error) could be
	/// applied to a companion that is not there any more. Called on the scene load, together with
	/// <see cref="CompanionMemory.ClearAll"/> and <see cref="CompanionCarry.Reset"/>; <see cref="LlmClient.Reset"/>
	/// drains the answer queue in the same step. Main thread only.
	/// </summary>
	internal static void Reset()
	{
		_awaitingResult = false;
		_pendingRequestId = 0;
		_pendingSpeaker = null;
		_pendingAddressee = null;
		// Fase 3.24 (auditoria): the 3 s cooldown belongs to the world it was started in - leaving and entering again
		// used to swallow the first order of the new world.
		_lastOrderTime = -999f;
		CompanionsPlugin.Log?.LogInfo("[IA] Estado pendente da camada de IA limpo (troca de mundo): nada da mensagem anterior sera aplicado.");
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
			CompanionsPlugin.Log?.LogInfo("[IA] The AI is off ([AI] Enabled = false) - message not sent");
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
		// Fase 3.11: when the message OPENs with the name of one of the player's companions ("Gruncha, colete 10
		// pedras"), that one is the addressee from here on - the prompt is built with HER name and personality, the
		// conversation memory is filed under her and the order is routed to her only (see HandleResult). This is read
		// from the player's own words, so it also works with a model that never fills the "companion" key.
		CompanionSetup addressee = CompanionNames.FromMessage(message, out string addresseeName);
		if (addressee != null)
		{
			CompanionsPlugin.Log?.LogInfo($"[IA] The message is addressed to \"{addresseeName}\" - the prompt and the order go to that companion");
		}
		CompanionSetup speaker = addressee ?? PickSpeaker();
		_pendingSpeaker = speaker;
		_pendingAddressee = addressee;
		_pendingName = NameOf(speaker);
		_pendingKey = KeyFor(speaker, _pendingName);

		Notify(ModLocalization.Loc("hc_ai_thinking"));
		// Fase 3.10: the personality of the companion being talked to - its own when the player gave it one, the
		// global [IA] one otherwise (CompanionProfile is the single place that decides)
		// Fase 3.19: the id comes back with the answer, so a late one can be recognized (see Tick)
		_pendingRequestId = LlmClient.Request(message, BuildPrompt(_pendingName, CompanionProfile.PersonaText(speaker), _pendingKey));
		_awaitingResult = _pendingRequestId > 0;
		if (!_awaitingResult)
		{
			CompanionsPlugin.Log?.LogWarning("[IA] A mensagem nao pode ir para o modelo: ja havia uma requisicao em andamento");
			if (announceRefusal)
			{
				Notify(ModLocalization.Loc("hc_ai_voice_busy"));
			}
			return false;
		}
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
	/// recent conversation. Built on the main thread, once per chat message - never per frame. Fase 3.11: it also
	/// lists the player's companions by name, which is what lets the model fill the "companion" key ("Gruncha,
	/// colete 10 pedras").
	/// </summary>
	private static string BuildPrompt(string companionName, string personality, string memoryKey)
	{
		string name = string.IsNullOrEmpty(companionName) ? ModLocalization.Loc("hc_ai_default_name") : companionName;
		string contract = Contract;
		StringBuilder builder = new StringBuilder(contract.Length + 600);
		builder.Append(Header(name, personality));
		// Fase 3.11: who the player has, by name - the only names the model may put in "companion". Empty when there
		// is nobody (the line is simply not there), and short by design: this is sent with every message.
		builder.Append(CompanionNames.PromptLine());
		builder.Append(contract);
		CompanionMemory.AppendTo(builder, memoryKey, name);
		return builder.ToString();
	}

	/// <summary>
	/// The lines in front of the contract: who this companion is, how it talks, and the two reminders about the spoken
	/// line. Multilanguage M3 fix: they are in the <b>language of the game</b> too (they were Portuguese only, like the
	/// contract), chosen by the same <see cref="CompanionLocale"/> table; anything without a text of its own gets
	/// English. Built once per message with the companion's own name and personality - never per frame.
	/// </summary>
	private static string Header(string name, string personality)
	{
		switch (CompanionLocale.Current.KokoroCode)
		{
		case "pt-br":
			return "Você é " + name + ", o companheiro NPC do jogador, e ele é o seu dono.\n"
				+ "Seu jeito de falar e de agir: " + personality + "\n"
				+ "Fale no idioma em que o jogador falar com você, no seu jeito, com UMA frase curta, e trate o jogador como seu dono.\n"
				+ "A sua fala é lida em voz alta para o jogador: uma frase curta sai na hora, uma frase longa faz ele esperar.\n";
		case "es":
			return "Eres " + name + ", el compañero NPC del jugador, y él es tu dueño.\n"
				+ "Tu manera de hablar y de actuar: " + personality + "\n"
				+ "Habla en el idioma en que el jugador te hable, a tu manera, con UNA frase corta, y trata al jugador como tu dueño.\n"
				+ "Lo que dices se lee en voz alta para el jugador: una frase corta sale enseguida, una larga le hace esperar.\n";
		case "fr":
			return "Tu es " + name + ", le compagnon PNJ du joueur, et il est ton maître.\n"
				+ "Ta façon de parler et d'agir : " + personality + "\n"
				+ "Parle dans la langue où le joueur te parle, à ta manière, avec UNE phrase courte, et traite le joueur comme ton maître.\n"
				+ "Ce que tu dis est lu à voix haute au joueur : une phrase courte sort tout de suite, une longue le fait attendre.\n";
		default:
			return "You are " + name + ", the player's NPC companion, and they are your owner.\n"
				+ "How you speak and behave: " + personality + "\n"
				+ "Speak in the language the player speaks to you, in your own way, in ONE short sentence, and treat the player as your owner.\n"
				+ "What you say is read out loud to the player: a short sentence comes out right away, a long one makes them wait.\n";
		}
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
		// the same name the player reads everywhere (ZDO -> Character), so the prompt and the memory use it too
		return CompanionNames.NameOf(companion);
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
		// Fase 3.19: the same words also FIX the opposite answer - the model has "inventory" as an action and used to
		// answer "inventory" for "fecha o inventário", which opened the window the player asked to close.
		if (intent != null && CompanionOrders.IsInventoryRequest(result.UserText, out bool closeInventory))
		{
			bool guessed = intent.IsNone;
			// Fase 3.23 (auditoria): both directions. Before this only "the model said inventory but the player asked
			// to close" was fixed - the opposite ("fecha o inventário" answered as inventory_close while the player
			// asked to open it) opened nothing at all.
			bool wrongWay = closeInventory
				? string.Equals(intent.Action, "inventory", StringComparison.Ordinal)
				: string.Equals(intent.Action, "inventory_close", StringComparison.Ordinal);
			if (guessed || wrongWay)
			{
				CompanionsPlugin.Log?.LogInfo($"[IA] The model answered \"{intent.Action}\" for \"{result.UserText}\", which asks to {(closeInventory ? "close" : "open")} the inventory - taking it as the \"{(closeInventory ? "inventory_close" : "inventory")}\" order (Fase 3.8/3.19 fallback)");
				intent.Action = closeInventory ? "inventory_close" : "inventory";
				intent.Target = null;
				intent.Resource = null;
			}
		}
		string reply = intent != null ? intent.Reply : null;
		// Fase M3 (bug do teste em francês): o modelo também devolveu "pickup" (e pode devolver "none") para
		// "ramasse 10 bois", porque o contrato do prompt estava só em PT-BR. Quando as palavras do jogador dizem
		// COLETAR (recurso + número, sem marcador de chão) e o modelo não entendeu nada, a coleta entra como ordem -
		// com a quantidade lida do texto. O caso "pickup" é corrigido no CompanionOrders.Run (uma linha de log só).
		if (intent != null && string.Equals(intent.Action, GatherIntent.NoneAction, StringComparison.Ordinal))
		{
			string byGather = GatherIntent.FromModelAction(intent.Action, result.UserText, out int gatherAmount);
			if (byGather != null)
			{
				CompanionsPlugin.Log?.LogInfo($"[IA] O modelo devolveu \"{intent.Action}\" mas o jogador pediu para coletar {CompanionOrders.ResourceWord(byGather)} - usei \"{byGather}\" ({gatherAmount}).");
				intent.Action = byGather;
				intent.Target = null;
				intent.Resource = null;
				intent.Amount = gatherAmount;
			}
		}
		// what the companion actually said (or did), kept in its memory so it can follow the thread
		string spoken = null;

		// Fase 3.11: who takes this answer (see ResolveAddressee: the player's words decide, the model's field is only
		// trusted when the message itself named somebody). Null = nobody was named -> exactly the old behaviour.
		CompanionSetup addressee = ResolveAddressee(intent, result.UserText, out bool stop);
		if (stop)
		{
			return;
		}
		// Fase 3.11/3.12: everything the companion says or does from here belongs to the named one; without a name it
		// is the speaker of the message, i.e. the old flow. Fase 3.12 (auditoria): the fallback goes through Live(),
		// because "??" does not use Unity's "destroyed == null" - a stale speaker would be used as if it were alive.
		// The memory key is NOT rewritten here any more: whoever speaks is the speaker of the turn (the named
		// companion is already the speaker since Submit), so the tone and the memory can never belong to two
		// different companions.
		CompanionSetup speaker = ReferenceEquals(addressee, null) ? Live(_pendingSpeaker) : addressee;

		if (intent == null || intent.IsNone)
		{
			// Fase 3.5: not an order - the companion answers as conversation, out loud and in the chat
			if (ConversationEnabled && !string.IsNullOrEmpty(reply))
			{
				string line = Shorten(reply, MaxReplyShown);
				CompanionsPlugin.Log?.LogInfo($"[IA] Conversation ({result.ElapsedMs} ms): \"{line}\"");
				Speak(speaker, line);
				Notify(line);
				// Fase 3.4: the same line is spoken (the reply is the whole line of a conversation, so there is no
				// "mod line" here to win over it)
				SpeakOutLoud(speaker, null, line);
				spoken = line;
			}
			else
			{
				CompanionsPlugin.Log?.LogInfo($"[IA] Not an order (action \"{intent?.Action}\") - nothing done");
				string line2 = ModLocalization.Loc("hc_ai_not_understood");
				Notify(line2);
				SpeakOutLoud(speaker, line2, reply);
			}
		}
		else
		{
			// Fase 3.6: attack/equip/drop have their own wording ("Atacando o troll.", "Não tenho picareta."), because
			// the feedback has to say WHICH creature or item it was about - the generic "Ok: ..." line can't.
			// Fase 3.11: a named companion is the only one the order reaches (addressee null = all of them, as before).
			CompanionOrders.OrderOutcome outcome = CompanionOrders.Run(intent, result.UserText, speaker, addressee);
			if (outcome == null || !outcome.Understood)
			{
				string line3 = ModLocalization.Loc("hc_ai_not_understood");
				Notify(line3);
				SpeakOutLoud(speaker, line3, reply);
			}
			else if (!string.IsNullOrEmpty(outcome.Message))
			{
				CompanionsPlugin.Log?.LogInfo($"[IA] \"{intent.Action}\"{(string.IsNullOrEmpty(intent.Target) ? "" : $" target=\"{intent.Target}\"")} -> {outcome.Count} companion(s) in {result.ElapsedMs} ms");
				Notify(outcome.Message);
				SpeakOutLoud(speaker, outcome.Message, reply);
				spoken = outcome.Message;
			}
			else if (outcome.Count == 0)
			{
				// Fase 3.2c: "colete 10 madeiras" with 15 already in the pack - the order was never started.
				// Fase 3.11: with a name the line says WHICH companion didn't take it (out of reach / orders off),
				// instead of the generic "no companion answered".
				string line4 = outcome.AlreadyHave
					? ModLocalization.Loc("hc_ai_already_have")
					: (addressee != null
						? ModLocalization.LocFmt("hc_ai_companion_not_available", CompanionNames.NameOf(addressee))
						: ModLocalization.Loc("hc_ai_no_companion"));
				Notify(line4);
				SpeakOutLoud(speaker, line4, reply);
			}
			else
			{
				string label = !string.IsNullOrEmpty(outcome.LabelArg)
					? ModLocalization.LocFmt(outcome.LabelKey, outcome.LabelArg)
					: ModLocalization.LocOr(outcome.LabelKey, intent.Action);
				string feedback = outcome.Amount > 0
					? ModLocalization.LocFmt("hc_ai_ok_amount", label, outcome.Amount)
					: ModLocalization.LocFmt("hc_ai_ok", label);
				CompanionsPlugin.Log?.LogInfo($"[IA] Applied \"{intent.Action}\" to {outcome.Count} companion(s) in {result.ElapsedMs} ms");
				Notify(feedback);
				SpeakOutLoud(speaker, feedback, reply);
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

	/// <summary>
	/// Fase 3.11 / 3.12: the companion this answer is for, and the single place the three cases are decided.
	///
	/// <list type="number">
	/// <item><b>the player named a real companion</b> ("Gruncha, colete 10 pedras", found by
	/// <see cref="CompanionNames.FromMessage"/> when the message was sent): that one, always - the model's
	/// <c>companion</c> key is not consulted (it can only confirm or disagree, and a small model copies example names);
	/// if it disagrees, that is one log line and nothing else. A companion that was destroyed between the message and
	/// the answer is reported ("that companion isn't here anymore") and nothing is ordered, instead of the order
	/// falling through to everybody;</item>
	/// <item><b>the message opens with a call to somebody who doesn't exist</b> ("Fulano, colete 10 pedras"): the
	/// player is told which name wasn't found and <paramref name="stop"/> comes back true - the order is not handed to
	/// every companion. The call is recognized by <see cref="CompanionNames.UnknownAddress"/>, which refuses order
	/// verbs, greetings and fillers ("bom dia, tudo bem?", "para, obrigado"), so ordinary talk is never caught
	/// here;</item>
	/// <item><b>the message names nobody</b> ("colete 10 pedras"): the model's <c>companion</c> key is <b>ignored</b>
	/// (one log line) and null comes back, i.e. exactly the behaviour before this feature - the order goes to every
	/// companion that can take it. A name the model invented, a "todos" or a "você" can never block an order the
	/// player did not address to anyone.</item>
	/// </list>
	/// </summary>
	/// <param name="userText">What the player said (typed or spoken), for case 2.</param>
	/// <param name="stop">true = say nothing else and apply nothing (the call named somebody that doesn't exist).</param>
	private static CompanionSetup ResolveAddressee(LlmIntent intent, string userText, out bool stop)
	{
		stop = false;
		bool namedByPlayer = !ReferenceEquals(_pendingAddressee, null);
		CompanionSetup pending = Live(_pendingAddressee);
		if (namedByPlayer && ReferenceEquals(pending, null))
		{
			// Fase 3.12 (auditoria): the ReferenceEquals above is what makes this branch reachable at all - Unity's
			// own != answers false for a DESTROYED object, so comparing it with == null never fired and the order
			// silently fell through to everybody.
			// the companion the player named left the world while the model was thinking (dismissed, dead and
			// destroyed, world change): say so instead of handing the order to everybody else
			Notify(ModLocalization.Loc("hc_ai_comp_gone"));
			CompanionsPlugin.Log?.LogInfo("[IA] The companion the message was addressed to is gone - nothing was ordered");
			stop = true;
			return null;
		}
		string modelName = intent != null ? intent.Companion : null;
		if (!ReferenceEquals(pending, null))
		{
			// case 1: the player named this one (the prompt, the persona and the memory are already its own)
			if (!string.IsNullOrEmpty(modelName) && !CompanionNameMatch.Same(modelName, CompanionNames.NameOf(pending)))
			{
				string known = ReferenceEquals(CompanionNames.Find(modelName), null) ? "isn't a companion" : "is another companion";
				CompanionsPlugin.Log?.LogInfo($"[IA] The model named \"{modelName}\" ({known}) but the player's message starts with \"{CompanionNames.NameOf(pending)}\" - the player's words win");
			}
			return pending;
		}
		string unknownCall = CompanionNames.UnknownAddress(userText);
		if (!string.IsNullOrEmpty(unknownCall))
		{
			// case 2: the message calls somebody the mod doesn't know - say it, and order nothing
			Notify(ModLocalization.LocFmt("hc_ai_unknown_companion", unknownCall));
			CompanionsPlugin.Log?.LogInfo($"[IA] The message is addressed to \"{unknownCall}\", which isn't one of the player's companions - nothing was ordered");
			stop = true;
			return null;
		}
		if (!string.IsNullOrEmpty(modelName))
		{
			// case 3: the player named nobody, so the model's field is not trusted (see the summary above)
			CompanionsPlugin.Log?.LogInfo($"[IA] The model answered companion=\"{modelName}\" but the message names nobody - ignoring it and running the order for all companions");
		}
		return null;
	}

	/// <summary>
	/// The companion, unless it is gone. Unity answers "null" for a destroyed object through its own <c>==</c>
	/// operator, which <c>??</c> does not use - so a field kept across the model call has to be asked this way.
	/// </summary>
	private static CompanionSetup Live(CompanionSetup companion)
	{
		return (companion != null) ? companion : null;
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
				// Fase 3.17: the AI path already sent this line to the TTS (SpeakOutLoud), so only the bubble is wanted
				talk.Say(text, force: true, withVoice: false);
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
	/// when <c>[AI] TtsSpeakModLines</c> is on;</item>
	/// <item><paramref name="reply"/> is the model's own wording and is spoken when there is no line to read or when
	/// <c>[AI] TtsSpeakModLines</c> is off, and only with <c>[AI] TtsSpeakReplies</c> on.</item>
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
