using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Companions;
// The game ships its own Console (the dev console) in assembly_valheim, and this file is outside any Valheim
// namespace: a bare "Console" binds to that one, so the harness writes through this alias.
using Out = System.Console;

namespace AiIntentsCheck;

/// <summary>
/// The word nets of the AI layer, checked <b>outside the game</b> against the real sources: ai-intents-check.csproj
/// compiles src/Companions/AI/{LlmIntent,RadialIntent,PickupIntent,GatherIntent,InteractionIntent,OrderItems,
/// CompanionNames,MHR_Owner,NavTileMath,BuildCatalog,BuildTemplates,BuildBlueprintParser,LevelIntent,BuildFailure,
/// BuildPieceKind,BuildSupportBelow}.cs and src/Companions/Core/MHR_List.cs straight into this executable, so every rule
/// asserted here is the shipped one (Stubs.cs holds only the Unity/config members those files touch).
///
/// <para>Two of those members cannot be linked (they live in classes that need the world), so they are copies in
/// Stubs.cs: <c>CompanionOrders.IsDoorRequest</c> and <c>CompanionOrders.JobMode</c>. Section (h) loads the built
/// Companions.dll and compares the copies with the real methods - the harness fails if they ever drift.</para>
///
/// <para>Section (p) is the <b>support gate</b> of the build (<c>BuildFailure.Classify</c>): the pure string → class table
/// that decides whether a piece that did not go up is waiting for the plan's own support (no attempt spent) or really
/// failed (one of the 3 attempts). Section (q) is the other pure table of the same job
/// (<c>BuildPieceKind.Classify</c>): structure or furniture, which is how far a piece may be moved from the plan's pose
/// when the spot refuses it. Section (r) is <c>BuildSupportBelow</c>: the window of the <b>direct support</b> of a piece,
/// which is the plan index a group drives next when one of its pieces is refused with <c>no_support</c>.</para>
///
/// <para>Run: <c>dotnet run --project tools/ai-intents-check</c> (after <c>dotnet build Companions.sln -c Release</c>,
/// which the cross-check reads). Exit code 0 only when every case passed and nothing was skipped.</para>
/// </summary>
internal static class Program
{
	private static int _pass;
	private static int _fail;
	private static int _skip;
	private static int _knownFail;

	private static readonly List<string> Failures = new List<string>();
	private static readonly List<string> Skips = new List<string>();

	private static readonly List<string> ProbeDirs = new List<string>();

	private static int Main()
	{
		try
		{
			Out.OutputEncoding = Encoding.UTF8;
		}
		catch
		{
			// redirected output without a console: the text is ASCII-safe enough
		}

		InstallAssemblyProbe();

		Out.WriteLine("AI Companions - harness das redes por palavras (net472, fontes reais de src/)");
		Out.WriteLine("pastas do jogo: " + (ProbeDirs.Count == 0 ? "(nenhuma encontrada)" : string.Join(" | ", ProbeDirs)));

		Section("(0) pre-condicoes: o harness roda FORA do jogo");
		Preconditions();
		if (_fail > 0)
		{
			Out.WriteLine();
			Out.WriteLine("FATAL: o ambiente do harness esta errado (veja acima). As assemblies do jogo sao procuradas em: "
				+ (ProbeDirs.Count == 0 ? "(nenhuma pasta)" : string.Join(" | ", ProbeDirs.ToArray())));
			return 2;
		}

		Section("(a) RadialIntent.Correct - postura, vagar, comando, trabalho e as guardas");
		Radial();
		Section("(b) PickupIntent.NeighborAction - nopickup que o jogador desmente");
		Pickup();
		Section("(c) GatherIntent.FromModelAction - coleta com numero");
		Gather();
		Section("(d) InteractionIntent.Correct - colmeia, porta, navio, sentar, bau");
		Interaction();
		Section("(e) LlmIntent.Parse / ExtractContent - o JSON do modelo");
		Parser();
		Section("(f) OrderItems.Resolve / GatherAction - o item da ordem nos 5 idiomas");
		Items();
		Section("(g) CompanionNameMatch - quem o jogador chamou (bonus)");
		Names();
		Section("(i) BuildCatalog - o nome da peca vira o prefab (E5: oficina)");
		Build();
		Section("(j) BuildTemplates - a estrutura por template (E6: cabana e abrigo)");
		Templates();
		Section("(k) LevelIntent - a ordem de nivelar o terreno (E10)");
		Leveling();
		Section("(l) BuildBlueprintParser - o .blueprint do PlanBuild (E7)");
		Blueprints();
		Section("(m) BuildBlueprintParser.NearestWithin - a ordem dentro do grupo (vizinho mais proximo)");
		BlueprintOrder();
		Section("(n) BuildBlueprintParser.Ordered - a chave do telhado (andar da laje que cobre)");
		BlueprintRoofKey();
		Section("(o) NavTileMath.Axis - o tile da navmesh (P2 da navegacao)");
		NavTiles();
		Section("(p) BuildFailure.Classify - o portao de apoio da obra (dependencia x falha real)");
		BuildFailures();
		Section("(q) BuildPieceKind.Classify - estrutura x mobilia (quanto a peca pode sair do plano)");
		PieceKinds();
		Section("(r) BuildSupportBelow - o apoio direto de uma peca (quem o grupo puxa para a frente)");
		SupportBelow();
		Section("(s) BuildBody - a janela do corpo na colocacao (blocked_by_player)");
		BodyWindows();
		CrossCheckDoorRule();

		Out.WriteLine();
		Out.WriteLine("=== " + _pass + " PASS / " + _fail + " FAIL ===");
		if (_knownFail > 0)
		{
			Out.WriteLine("(" + _knownFail + " KNOWN-FAIL: o comportamento esperado e o bug - nao contam como PASS/FAIL)");
		}
		if (_skip > 0)
		{
			Out.WriteLine("(" + _skip + " SKIP)");
			for (int i = 0; i < Skips.Count; i++)
			{
				Out.WriteLine("  skip: " + Skips[i]);
			}
		}
		if (_fail > 0)
		{
			Out.WriteLine();
			Out.WriteLine("falhas:");
			for (int i = 0; i < Failures.Count; i++)
			{
				Out.WriteLine("  - " + Failures[i]);
			}
		}
		if (_fail == 0 && _skip == 0)
		{
			Out.WriteLine("TODOS OS TESTES PASSARAM");
			return 0;
		}
		return 1;
	}

	// ============================================================================================ the sections

	private static void Radial()
	{
		RadialCase("postura equilibrada", "none", "stance_balanced", "ficar na postura equilibrada");
		RadialCase("fica na postura defensiva", "wander", "stance_passive", "ficar na postura defensiva");
		RadialCase("posizione a distanza", "none", "stance_ranged", "ficar na postura à distância");
		RadialCase("fica na postura corpo a corpo", "none", "stance_melee", "ficar na postura corpo a corpo");
		RadialCase("para de vagar", "stop", "nowander", "parar de vagar");
		RadialCase("vagueia por aí", "none", "wander", "vagar");
		RadialCase("ignora os comandos", "none", "nocommand", "ignorar os comandos");
		RadialCase("aceita os comandos", "stop", "command", "aceitar comandos");
		RadialCase("acende o forno", "repair", "smelt", "fundir minério");
		RadialCase("cuida da cozinha", "farm", "cook", "cozinhar");
		RadialCase("vai caçar", "wander", "hunt", "caçar");
		// guards
		RadialCase("caça o greydwarf", "farm", null);
		RadialCase("conserta a cozinha", "farm", null);
		RadialCase("ataca o troll na postura defensiva", "attack", null);
		RadialCase("postura equilibrada", "wood", null);
		RadialCase("postura equilibrada", "stance_balanced", null);
		RadialCase("para de vagar?", "stop", null);
		RadialCase("para de vagar por aí agora mesmo e volta para casa comigo já", "stop", null);
		RadialCase("", "stop", null);
		RadialCase("postura equilibrada", null, null);
	}

	private static void Pickup()
	{
		PickupCase("jogue fora a madeira", "drop");
		PickupCase("junte a madeira do chão", "pickup");
		PickupCase("larga a pedra", "drop");
		PickupCase("deixa no chão", "drop");
		PickupCase("butta via la legna", "drop");
		PickupCase("pega os itens do chão", "pickup");
		PickupCase("recolhe a madeira do chão", "pickup");
		// the legitimate nopickup, and the ones the player negates: nothing is corrected
		PickupCase("para de recolher", null);
		PickupCase("para", null);
		PickupCase("não pegue nada", null);
		PickupCase("não jogue fora a madeira", null);
		PickupCase("não quero que você jogue fora a madeira", null);
		PickupCase("largada", null);
		// only nopickup is read by this net
		Check("NeighborAction(\"drop\", \"jogue fora a madeira\") nao mexe em outra acao",
			PickupIntent.NeighborAction("drop", "jogue fora a madeira") == null);
		Check("NeighborAction(\"pickup\", \"junte a madeira\") nao mexe em outra acao",
			PickupIntent.NeighborAction("pickup", "junte a madeira") == null);
	}

	private static void Gather()
	{
		GatherCase("none", "colete 10 pedras", "stone", 10);
		GatherCase("pickup", "colete 20 de madeira", "wood", 20);
		GatherCase("none", "junta 7 pedras", "stone", 7);
		GatherCase("pickup", "ramasse 10 bois", "wood", 10);
		GatherCase("pickup", "raccogli 5 pietre", "stone", 5);
		GatherCase("pickup", "minere 12 minérios", "ore", 12);
		GatherCase("pickup", "colhe 5 framboesas", "forage", 5);
		// the legitimate pickup (something on the ground) is never turned into a gathering order
		GatherCase("pickup", "pega a madeira do chão", null, 0);
		GatherCase("pickup", "ramasse 10 bois par terre", null, 0);
		// no number, no order; and the "none" side needs a collecting verb ("você tem 10 madeiras?")
		GatherCase("none", "colete pedras", null, 0);
		GatherCase("none", "você tem 10 madeiras?", null, 0);
		GatherCase("nopickup", "colete 10 pedras", null, 0);
		CheckInt("GatherIntent.NumberIn(\"ramasse 10 bois\")", 10, GatherIntent.NumberIn(LlmIntent.Normalize("ramasse 10 bois")));
		CheckInt("GatherIntent.NumberIn(\"nada\")", 0, GatherIntent.NumberIn("nada"));
		CheckInt("GatherIntent.NumberIn(\"12345\") para em 3 digitos", 123, GatherIntent.NumberIn("12345"));
	}

	private static void Interaction()
	{
		InteractionCase("inventory_close", "fecha a porta", "door", "fechar a porta");
		InteractionCase("inventory", "abre a porta", "door", null);
		InteractionCase("none", "senta na fogueira", "sit", "sentar perto do fogo");
		InteractionCase("drop", "guarda isso no baú", "deposit", "guardar no bau");
		InteractionCase("none", "esvazia a colmeia", "hive", "esvaziar a colmeia");
		InteractionCase("none", "sobe no barco", "board", "embarcar no navio");
		// refusals
		InteractionCase("none", "fecha a porta?", null, null);
		InteractionCase("none", "para de guardar no baú", null, null);
		InteractionCase("none", "não guarda nada no baú", null, null);
		InteractionCase("drop", "joga a madeira no chão", null, null);
		InteractionCase("attack", "senta na fogueira", null, null);
		InteractionCase("door", "fecha a porta", null, null);
		InteractionCase("inventory_close", "abre o teu inventário e fecha a porta", null, null);
		InteractionCase("none", "o que tem no teu inventário?", null, null);
		InteractionCase("none", "", null, null);
		// the door rule the net delegates to (Stubs.cs copy - cross-checked against the DLL in section (h))
		Check("IsDoorRequest(\"fecha a porta\")", CompanionOrders.IsDoorRequest("fecha a porta"));
		Check("IsDoorRequest(\"abre o teu inventário e fecha a porta\") == false (a mochila manda)",
			!CompanionOrders.IsDoorRequest("abre o teu inventário e fecha a porta"));
		Check("IsDoorRequest(\"transportar madeira\") == false (palavra inteira)",
			!CompanionOrders.IsDoorRequest("transportar madeira"));
	}

	private static void Parser()
	{
		LlmIntent wood = LlmIntent.Parse("{\"action\":\"wood\",\"amount\":10}");
		CheckText("Parse(json).Action", "wood", wood.Action);
		CheckInt("Parse(json).Amount", 10, wood.Amount);
		Check("Parse(json).Resource == null", wood.Resource == null);

		LlmIntent quoted = LlmIntent.Parse("{\"action\":\"stone\",\"resource\":\"stone\",\"amount\":\"5\"}");
		CheckText("Parse(amount entre aspas).Action", "stone", quoted.Action);
		CheckInt("Parse(amount entre aspas).Amount", 5, quoted.Amount);
		CheckText("Parse(amount entre aspas).Resource", "stone", quoted.Resource);

		LlmIntent prose = LlmIntent.Parse("Claro! {\"action\":\"attack\",\"target\":\"Troll\"} e boa sorte");
		CheckText("Parse(json com texto em volta).Action", "attack", prose.Action);
		CheckText("Parse(json com texto em volta).Target", "troll", prose.Target);
		Check("Parse(json com texto em volta).Resource == null (o alvo virou target)", prose.Resource == null);

		CheckText("Parse(acao desconhecida) -> none", "none", LlmIntent.Parse("{\"action\":\"dance\"}").Action);
		CheckText("Parse(action \"null\") -> none", "none", LlmIntent.Parse("{\"action\":\"null\"}").Action);
		CheckText("Parse(sem json, uma palavra) -> a acao", "wood", LlmIntent.Parse("wood").Action);
		CheckText("Parse(vazio) -> none", "none", LlmIntent.Parse("").Action);
		CheckText("Parse(null) -> none", "none", LlmIntent.Parse(null).Action);

		LlmIntent badAmount = LlmIntent.Parse("{\"action\":\"wood\",\"amount\":\"muitas\"}");
		CheckText("Parse(amount invalido).Action", "wood", badAmount.Action);
		CheckInt("Parse(amount invalido).Amount == 0", 0, badAmount.Amount);

		LlmIntent negative = LlmIntent.Parse("{\"action\":\"wood\",\"amount\":-5}");
		CheckInt("Parse(amount negativo).Amount == 0", 0, negative.Amount);

		LlmIntent door = LlmIntent.Parse("{\"action\":\"fecha_a_porta\"}");
		CheckText("Parse(acao que nomeia a porta)", "door", door.Action);
		LlmIntent equip = LlmIntent.Parse("{\"action\":\"equip\",\"target\":\"Machado de Bronze\"}");
		CheckText("Parse(equip).Target normalizado", "machado_de_bronze", equip.Target);
		LlmIntent drop = LlmIntent.Parse("{\"action\":\"drop\",\"resource\":\"pedra\"}");
		CheckText("Parse(drop com resource): o alvo vem do resource", "pedra", drop.Target);
		Check("Parse(drop com resource).Resource == null", drop.Resource == null);

		LlmIntent chat = LlmIntent.Parse("{\"reply\":\"Bom dia!\"}");
		CheckText("Parse(so reply).Action", "none", chat.Action);
		CheckText("Parse(so reply).Reply", "Bom dia!", chat.Reply);

		LlmIntent pickup = LlmIntent.Parse("{\"action\":\"pickup\"}");
		CheckText("Parse(pickup)", "pickup", pickup.Action);

		string body = "{\"choices\":[{\"message\":{\"content\":\"{\\\"action\\\":\\\"stone\\\",\\\"amount\\\":3}\"}}]}";
		string content = LlmIntent.ExtractContent(body);
		CheckText("ExtractContent(t corpo do chat completion)", "{\"action\":\"stone\",\"amount\":3}", content);
		LlmIntent fromBody = LlmIntent.Parse(content);
		CheckText("Parse(ExtractContent(...)).Action", "stone", fromBody.Action);
		CheckInt("Parse(ExtractContent(...)).Amount", 3, fromBody.Amount);

		Check("IsIntentWord(\"colete\")", LlmIntent.IsIntentWord("colete"));
		Check("IsIntentWord(\"Fulano\") == false", !LlmIntent.IsIntentWord("Fulano"));
	}

	private static void Items()
	{
		// (f) the same resource, in the five languages of the mod
		ItemCase("stone", "pedra", 0, "Stone");           // pt
		ItemCase("stone", "pedras", 0, "Stone");
		ItemCase("wood", "madeira", 0, "Wood");
		ItemCase("wood", "casca de ancião", 0, "ElderBark");
		ItemCase("wood", "wood", 0, "Wood");              // en
		ItemCase("ore", "iron", 0, "IronScrap");
		ItemCase("wood", "madera", 0, "Wood");            // es
		ItemCase("stone", "piedras", 0, "Stone");
		ItemCase("ore", "plata", 0, "SilverOre");
		ItemCase("wood", "bois", 0, "Wood");              // fr
		ItemCase("stone", "pierre", 0, "Stone");
		ItemCase("ore", "fer", 0, "IronScrap");
		ItemCase("stone", "pietre", 0, "Stone");          // it
		ItemCase("wood", "legna", 0, "Wood");
		ItemCase("ore", "rame", 0, "CopperOre");
		// forage, through the alias map
		ItemCase("forage", "framboesa", 0, "Raspberry");
		ItemCase("forage", "frambuesas", 0, "Raspberry");
		ItemCase("forage", "champignons", 0, "Mushroom");
		ItemCase("forage", "funghi", 0, "Mushroom");
		// no resource named: the action is the resource ("corte 10 madeiras")
		ItemCase("wood", null, 0, "Wood");
		ItemCase("stone", null, 0, "Stone");
		ItemCase("ore", null, 0, null);        // "ore" is copper/iron/silver at once: the whole job
		ItemCase("forage", null, 0, null);
		ItemCase("wood", "diamante", 0, null); // named something the game doesn't know: the job's list decides

		// the job's own list is the last place looked at (a name that is not in the alias map)
		string original = ModConfig.GatherStone.Value;
		try
		{
			ModConfig.GatherStone.Value = "Stone, Cloudberry";
			ItemCase("stone", "cloudberry", 2, "Cloudberry");
		}
		finally
		{
			ModConfig.GatherStone.Value = original;
		}

		// the resource wins over the generic action (Fase 3.1)
		CheckText("GatherAction(\"wood\", \"stone\") -> o recurso manda", "stone", OrderItems.GatherAction("wood", "stone"));
		CheckText("GatherAction(\"wood\", \"pedra\") -> o recurso manda (pt)", "stone", OrderItems.GatherAction("wood", "pedra"));
		CheckText("GatherAction(\"forage\", \"pedra\") -> stone", "stone", OrderItems.GatherAction("forage", "pedra"));
		Check("GatherAction(\"stone\", \"stone\") == null (par coerente)", OrderItems.GatherAction("stone", "stone") == null);
		Check("GatherAction(\"attack\", \"stone\") == null (nao e coleta)", OrderItems.GatherAction("attack", "stone") == null);
		Check("GatherAction(\"wood\", null) == null", OrderItems.GatherAction("wood", null) == null);
		Check("GatherAction(\"wood\", \"xyz\") == null", OrderItems.GatherAction("wood", "xyz") == null);
	}

	/// <summary>
	/// Section (i): the recipe resolver of the build order (E5). It is pure - the whole point of the design - so the
	/// names the player may say can be tested outside the game, in the five languages, with the plurals and the
	/// longest-alias rule ("cama de dragão" must not fall into "cama").
	/// </summary>
	private static void Build()
	{
		// Portuguese: the four pieces of the workshop, and the dragon bed winning over the simple one
		BuildCase("construa uma bancada de trabalho aqui", "piece_workbench");
		BuildCase("faz um baú aí", "piece_chest_wood");
		BuildCase("acende uma fogueira", "fire_pit");
		BuildCase("coloca uma cama", "bed");
		BuildCase("constrói a cama de dragão", "piece_bed02");
		BuildCase("monta um baú de madeira", "piece_chest_wood");
		// plurals (the first E5 test would have failed silently on "duas camas")
		BuildCase("constrói duas camas", "bed");
		BuildCase("faz duas bancadas", "piece_workbench");
		BuildCase("dois baús", "piece_chest_wood");
		BuildCase("três fogueiras", "fire_pit");
		// accents and case are folded
		BuildCase("BAÚ", "piece_chest_wood");
		BuildCase("BAU", "piece_chest_wood");
		BuildCase("CAMA-DE-DRAGAO", "piece_bed02");
		// English
		BuildCase("build a workbench here", "piece_workbench");
		BuildCase("place a wooden chest", "piece_chest_wood");
		BuildCase("make a campfire", "fire_pit");
		BuildCase("build two beds", "bed");
		BuildCase("the dragon bed", "piece_bed02");
		// Spanish
		BuildCase("construye un banco de trabajo", "piece_workbench");
		BuildCase("pon un cofre de madera", "piece_chest_wood");
		BuildCase("haz una fogata", "fire_pit");
		BuildCase("una cama sencilla", "bed");
		// French
		BuildCase("construis un établi", "piece_workbench");
		BuildCase("un coffre en bois", "piece_chest_wood");
		BuildCase("un feu de camp", "fire_pit");
		BuildCase("deux lits", "bed");
		// Italian
		BuildCase("costruisci un banco da lavoro", "piece_workbench");
		BuildCase("una cassa", "piece_chest_wood");
		BuildCase("un falò", "fire_pit");
		BuildCase("due letti", "bed");

		// nothing named / something outside the table: null (the order refuses with its own line)
		Check("Resolve(\"constrói uma parede\") == null (fora da allow-list)", BuildCatalog.Resolve("constrói uma parede") == null);
		Check("Resolve(\"\") == null", BuildCatalog.Resolve(string.Empty) == null);
		Check("Resolve(null) == null", BuildCatalog.Resolve(null) == null);
		// whole words only: "camada" is not "cama"
		Check("Resolve(\"uma camada de terra\") == null (palavra inteira)", BuildCatalog.Resolve("uma camada de terra") == null);
		Check("Resolve(\"bancadinha\") == null (palavra inteira)", BuildCatalog.Resolve("uma bancadinha") == null);

		// the allow-list gate the order uses
		foreach (string prefab in BuildCatalog.Prefabs())
		{
			Check("Allowed(\"" + prefab + "\")", BuildCatalog.Allowed(prefab));
		}
		Check("Prefabs() tem 5 pecas", BuildCatalog.Prefabs().Length == 5);
		Check("Allowed(\"wood_wall\") == false", !BuildCatalog.Allowed("wood_wall"));
		Check("Allowed(null) == false", !BuildCatalog.Allowed(null));

		// the label used in the log/refusal
		CheckText("Label(piece_workbench)", "bancada de trabalho", BuildCatalog.Label("piece_workbench"));
		CheckText("Label(piece_bed02)", "cama de dragão", BuildCatalog.Label("piece_bed02"));
		CheckText("Label(desconhecida) devolve o proprio nome", "xyz", BuildCatalog.Label("xyz"));

		// the word helpers the order uses to read the sentence
		Check("HasWord(\"constrói uma cama aqui\", \"cama\")", BuildCatalog.HasWord("constrói uma cama aqui", "cama"));
		Check("HasWord(\"camada\", \"cama\") == false", !BuildCatalog.HasWord("camada", "cama"));
		Check("HasAnyWord(\"faz um baú\", \"cama\", \"bau\")", BuildCatalog.HasAnyWord("faz um bau", "cama", "bau"));
	}

	/// <summary>
	/// Section (j): the templates of E6 - the aliases in the five languages, the build order (foundation, walls,
	/// roof) and the composition of each structure. Pure, so it runs outside the game.
	/// </summary>
	/// <summary>
	/// Section (k): the leveling order (E10) - the verb in the five languages and the size the player said. Pure, so it
	/// runs outside the game.
	/// </summary>
	private static void Leveling()
	{
		// the request, in the five languages
		Check("nivele um terreno 10x10 aqui", LevelIntent.IsRequest("nivele um terreno 10x10 aqui"));
		Check("nivela o chão aí", LevelIntent.IsRequest("nivela o chão aí"));
		Check("aplana o terreno", LevelIntent.IsRequest("aplana o terreno"));
		Check("level the ground 12 by 12", LevelIntent.IsRequest("level the ground 12 by 12"));
		Check("flatten the area", LevelIntent.IsRequest("flatten the area"));
		Check("allana el terreno", LevelIntent.IsRequest("allana el terreno"));
		Check("aplatir le terrain", LevelIntent.IsRequest("aplatir le terrain"));
		Check("livella il terreno", LevelIntent.IsRequest("livella il terreno"));
		// and what is NOT a leveling order
		Check("bom dia, tudo bem? nao e nivelar", !LevelIntent.IsRequest("bom dia, tudo bem?"));
		Check("constroi uma casa aqui nao e nivelar", !LevelIntent.IsRequest("constroi uma casa aqui"));
		Check("colete 10 madeiras nao e nivelar", !LevelIntent.IsRequest("colete 10 madeiras"));
		Check("null nao e nivelar", !LevelIntent.IsRequest(null));

		// the size, in the ways the player writes it
		CheckFloat("10x10 -> 10", 10f, LevelIntent.Size("nivele um terreno 10x10 aqui", 8f));
		CheckFloat("6 x 6 -> 6", 6f, LevelIntent.Size("nivela o chão 6 x 6", 8f));
		CheckFloat("12 por 12 -> 12", 12f, LevelIntent.Size("aplana 12 por 12", 8f));
		CheckFloat("12 by 12 -> 12", 12f, LevelIntent.Size("level the ground 12 by 12", 8f));
		CheckFloat("8m -> 8", 8f, LevelIntent.Size("nivela 8m aqui", 5f));
		CheckFloat("sem tamanho -> o padrao", 8f, LevelIntent.Size("nivele o terreno aqui", 8f));
		CheckFloat("numero pequeno demais nao vale", 8f, LevelIntent.Size("nivela 1x1", 8f));
		CheckFloat("numero grande demais nao vale", 8f, LevelIntent.Size("nivela 99x99", 8f));
	}


	/// <summary>
	/// E7: the .blueprint reader. The format is PlanBuild's (BiomeBlueprints/valheimians): text with the headers and
	/// sections, and one line per piece. The tests are hermetic (the text is here, not a file on disk).
	/// </summary>
	private static void Blueprints()
	{
		string name;
		string error;
		int broken;
		System.Collections.Generic.List<BuildBlueprintParser.Line> pieces;

		// V2: headers + sections; only #Pieces lines are pieces
		string v2 = string.Join("\n", new string[] {
			"#Name: Casa Viking",
			"#Creator: somebody",
			"#Description:",
			"two lines of text",
			"#Category: Blueprints",
			"#SnapPoints",
			"1;2;3",
			"-0.7653809;4;1.847778",
			"#Terrain",
			"4;5;6",
			"#Pieces",
			"wood_floor;Building;0;0;0;0;0;0;1;",
			"woodwall;Building;1;1;0;0;0;0;1;",
			"wood_roof_45(Clone);Building;2;3;0;0;0;0;1;;1;2;3",
			"#TerrainHeight",
			"99;99;99"
		});
		bool ok = BuildBlueprintParser.Parse(v2.Split('\n'), out name, out pieces, out error, out broken);
		Check("V2: leu", ok);
		CheckText("V2: nome do #Name", "Casa Viking", name);
		CheckInt("V2: 3 pecas (#SnapPoints/#Terrain/#TerrainHeight fora)", 3, pieces.Count);
		CheckInt("V2: nenhuma linha quebrada", 0, broken);
		CheckText("V2: (Clone) fora do prefab", "wood_roof_45", pieces[2].Prefab);
		CheckFloat("V2: px", 2f, pieces[2].Px);
		CheckFloat("V2: py", 3f, pieces[2].Py);
		CheckFloat("V2: escala x", 1f, pieces[2].Scale);
		CheckFloat("V2: rotacao w", 1f, pieces[2].Rw);

		// V1: no headers at all - every line is a piece (the old format, from the player's own capture)
		string v1 = string.Join("\n", new string[] {
			"stone_floor_2x2;Building;1;0;1;0;1;0;-4.371139E-08;",
			"woodwall;Building;0;1.5;1;0;0.7071068;0;-0.7071068;"
		});
		ok = BuildBlueprintParser.Parse(v1.Split('\n'), out name, out pieces, out error, out broken);
		Check("V1: leu", ok);
		CheckInt("V1: 2 pecas", 2, pieces.Count);
		CheckText("V1: nome nulo (nao tem cabecalho)", "", name ?? "");
		CheckFloat("V1: py da parede", 1.5f, pieces[1].Py);

		// virgula decimal (arquivos antigos)
		string commas = "wood_floor;Building;1,5;0,25;2,75;0;0;0;1;";
		ok = BuildBlueprintParser.Parse(new string[] { commas }, out name, out pieces, out error, out broken);
		Check("virgula: leu", ok);
		CheckFloat("virgula: px 1,5", 1.5f, pieces[0].Px);
		CheckFloat("virgula: pz 2,75", 2.75f, pieces[0].Pz);

		// linha quebrada e contada, nao entra
		ok = BuildBlueprintParser.Parse(new string[] { "wood_floor;Building;0;0", "woodwall;Building;0;1;0;0;0;0;1;" },
			out name, out pieces, out error, out broken);
		Check("quebrada: ainda leu a boa", ok);
		CheckInt("quebrada: 1 peca", 1, pieces.Count);
		CheckInt("quebrada: 1 contada", 1, broken);

		// vazio
		ok = BuildBlueprintParser.Parse(new string[] { "#Name: x", "#Pieces" }, out name, out pieces, out error, out broken);
		Check("vazio: recusa", !ok);
		CheckText("vazio: motivo", "no pieces", error);
		ok = BuildBlueprintParser.Parse(null, out name, out pieces, out error, out broken);
		Check("null: recusa", !ok);

		// ordem: sobe por altura (e estavel)
		BuildBlueprintParser.Line a = new BuildBlueprintParser.Line { Prefab = "roof", Py = 4f };
		BuildBlueprintParser.Line b = new BuildBlueprintParser.Line { Prefab = "floor", Py = 0f };
		BuildBlueprintParser.Line c = new BuildBlueprintParser.Line { Prefab = "wall", Py = 2f };
		BuildBlueprintParser.Line d = new BuildBlueprintParser.Line { Prefab = "wall2", Py = 2f };
		System.Collections.Generic.List<BuildBlueprintParser.Line> ordered = BuildBlueprintParser.Ordered(
			new System.Collections.Generic.List<BuildBlueprintParser.Line> { a, b, c, d });
		CheckText("ordem: piso primeiro", "floor", ordered[0].Prefab);
		CheckText("ordem: telhado por ultimo", "roof", ordered[3].Prefab);
		CheckText("ordem: empate mantem a ordem do arquivo", "wall", ordered[1].Prefab);
	}

	/// <summary>
	/// Section (m): E7 (player's report, 03/11/2026: "he walks back and forth across the build even on the ground
	/// floor"). The four sort keys decide the GROUPS; inside one run of pieces that share them (door flag, storey,
	/// class and height +-0.01) the order is the greedy nearest-neighbour path from where the previous run ended.
	/// What this section pins: the path is short (no zigzag), the groups are not mixed (a piece never changes group,
	/// none is added or lost), the doors stay last, a one-piece run is not touched, and the same input twice gives the
	/// same output. Pure, so every plan here is written by hand - there is no .blueprint file.
	/// </summary>
	private static void BlueprintOrder()
	{
		// (a) the zigzag: the file alternates between the two sides of a 10 m wide, 1 m deep floor
		System.Collections.Generic.List<BuildBlueprintParser.Line> zigzag = ZigzagPlan();
		System.Collections.Generic.List<BuildBlueprintParser.Line> zigzagOrder = BuildBlueprintParser.Ordered(zigzag);
		CheckInt("vizinho: 4 pisos, 4 pecas na saida", 4, zigzagOrder.Count);
		Check("vizinho: nenhuma peca inventada nem perdida", SamePieces(zigzag, zigzagOrder));
		CheckText("vizinho: caminho curto a partir do 1o piso", "0,0 0,1 10,1 10,0", TraceXZ(zigzagOrder));
		CheckFloat("vizinho: soma do caminho 12 m (10 de largura + 1 de fundo, nao 3 travessias)", 12f, PathLength(zigzagOrder));
		CheckInt("vizinho: atravessa a obra UMA vez (a ordem do arquivo atravessava 3)", 1, LongJumps(zigzagOrder, 5f));

		// a tolerancia da chave: 5 mm de altura nao criam outro run (o limite e +-0,01)
		System.Collections.Generic.List<BuildBlueprintParser.Line> jitter = Plan(
			Piece("wood_floor", 0f, 0f, 0f),
			Piece("wood_floor", 10f, 0.005f, 0f),
			Piece("wood_floor", 0f, 0f, 1f),
			Piece("wood_floor", 10f, 0.005f, 1f));
		CheckText("vizinho: 5 mm de altura nao abrem outro grupo", "0,0 0,1 10,1 10,0",
			TraceXZ(BuildBlueprintParser.Ordered(jitter)));

		// (b) two groups that are not mixed: 2 floors at y=0 and 2 at y=4, interleaved in the file on purpose
		System.Collections.Generic.List<BuildBlueprintParser.Line> groups = Plan(
			Piece("wood_floor", 0f, 0f, 0f),
			Piece("wood_floor", 0f, 4f, 0f),
			Piece("wood_floor", 2f, 0f, 0f),
			Piece("wood_floor", 2f, 4f, 0f));
		System.Collections.Generic.List<BuildBlueprintParser.Line> groupsOrder = BuildBlueprintParser.Ordered(groups);
		CheckText("grupos: os dois de y=0 antes dos dois de y=4", "0 0 4 4", TraceY(groupsOrder));
		CheckText("grupos: o andar de cima comeca onde o de baixo terminou", "0,0 2,0 2,0 0,0", TraceXZ(groupsOrder));

		// (c) the door is written in the middle of the file and still goes last
		System.Collections.Generic.List<BuildBlueprintParser.Line> doorPlan = Plan(
			Piece("woodwall", 0f, 0f, 0f),
			Piece("wood_door", 4f, 0f, 0f),
			Piece("woodwall", 1f, 0f, 0f),
			Piece("woodwall", 2f, 0f, 0f));
		System.Collections.Generic.List<BuildBlueprintParser.Line> doorOrder = BuildBlueprintParser.Ordered(doorPlan);
		CheckText("porta: por ultimo", "wood_door", doorOrder[doorOrder.Count - 1].Prefab);
		CheckText("porta: as 3 paredes em caminho antes dela", "woodwall woodwall woodwall wood_door", TracePrefabs(doorOrder));
		CheckInt("porta: nada de outra classe depois da 1a porta", 0, NotDoorAfterFirstDoor(doorOrder));

		// and the run of the doors itself follows the path (the first door of the file is the FAR one:
		// the walk starts where the last wall ended, not at the top of the file)
		System.Collections.Generic.List<BuildBlueprintParser.Line> twoDoors = Plan(
			Piece("wood_door", -8f, 0f, 0f),
			Piece("woodwall", 0f, 0f, 0f),
			Piece("wood_door", 8f, 0f, 0f),
			Piece("woodwall", 1f, 0f, 0f));
		CheckText("porta: o run das portas sai em caminho (a 1a do arquivo e a mais longe)", "0,0 1,0 8,0 -8,0",
			TraceXZ(BuildBlueprintParser.Ordered(twoDoors)));

		// (d) a run of ONE piece is not touched: the stair keeps the place its class gives it
		System.Collections.Generic.List<BuildBlueprintParser.Line> single = Plan(
			Piece("woodwall", 9f, 0f, 9f),
			Piece("wood_floor", 0f, 0f, 0f),
			Piece("wood_stair", 5f, 0f, 0f),
			Piece("wood_floor", 1f, 0f, 0f));
		System.Collections.Generic.List<BuildBlueprintParser.Line> singleOrder = BuildBlueprintParser.Ordered(single);
		CheckText("run de 1: piso -> escada -> parede (a classe manda)", "wood_floor wood_floor wood_stair woodwall",
			TracePrefabs(singleOrder));
		CheckInt("run de 1: a escada fica no indice que a classe dela da", 2, IndexOfPrefab(singleOrder, "wood_stair"));
		System.Collections.Generic.List<BuildBlueprintParser.Line> tiny = Plan(
			Piece("woodwall", 3f, 0f, 3f),
			Piece("wood_floor", 0f, 0f, 0f));
		CheckText("run de 1: com 2 pecas so a classe decide", "wood_floor woodwall",
			TracePrefabs(BuildBlueprintParser.Ordered(tiny)));

		// the guarantee added in review (03/11/2026): the walk never comes out worse than the .blueprint - a run the
		// greedy would LENGTHEN keeps the file order. Here the tie trap: the previous run ends at x=10, the walls are
		// 0/10/20, and the walk 10 -> 0 -> 20 (30 m) is longer than the file 0 -> 10 -> 20 (20 m).
		System.Collections.Generic.List<BuildBlueprintParser.Line> trap = Plan(
			Piece("wood_floor", 10f, 0f, 0f),
			Piece("woodwall", 0f, 0f, 0f),
			Piece("woodwall", 10f, 0f, 0f),
			Piece("woodwall", 20f, 0f, 0f));
		CheckText("nunca pior: guloso de 30 m perde para o arquivo de 20 m (o arquivo fica)", "10,0 0,0 10,0 20,0",
			TraceXZ(BuildBlueprintParser.Ordered(trap)));

		// (e) stability: the same input twice gives the same output, and the caller's list is not reordered
		System.Collections.Generic.List<BuildBlueprintParser.Line> first = BuildBlueprintParser.Ordered(ZigzagPlan());
		System.Collections.Generic.List<BuildBlueprintParser.Line> second = BuildBlueprintParser.Ordered(ZigzagPlan());
		CheckText("estabilidade: duas listas iguais dao a mesma ordem", TraceXZ(first), TraceXZ(second));
		System.Collections.Generic.List<BuildBlueprintParser.Line> again = BuildBlueprintParser.Ordered(zigzag);
		Check("estabilidade: Ordered() na MESMA lista devolve a mesma ordem", SameOrder(zigzagOrder, again));
		CheckText("estabilidade: a lista do chamador continua na ordem do arquivo", "0,0 10,0 0,1 10,1", TraceXZ(zigzag));
	}

	/// <summary>
	/// Section (n): E7 (review, 03/11/2026) - the storey key of a roof/gable piece (class 3) is the <b>slab it covers</b>:
	/// the nearest floor ABOVE it (0.3-6 m up, 2.5 m in XZ). Every case here is a handicap the older keys fail: the plain
	/// <c>StoreyOf</c> puts the roof in the lower storey (cases a and c - it comes out BEFORE the floor it covers), and
	/// the short-lived <c>StoreyOf + 2</c> only reached a 2 m ceiling, not the 2.5-3 m of the game (case a has the floor
	/// at 3 m). The floor that covers the roof wins by CLASS (the keys tie), no piece is moved between storeys, a roof
	/// with no slab above falls back to the storey below, and a floor out of the window does not count. Pure, like
	/// section (m): every plan is written by hand.
	/// </summary>
	private static void BlueprintRoofKey()
	{
		// (a) the roof is written BEFORE the slab it covers, one real storey (3 m) above it: the floor comes first -
		// and it comes first by CLASS, because both resolve to the same storey key (the floor is "above", not "below").
		System.Collections.Generic.List<BuildBlueprintParser.Line> covered = Plan(
			Piece("wood_roof_45", 0f, 0f, 0f),
			Piece("wood_floor", 0f, 3f, 0f));
		System.Collections.Generic.List<BuildBlueprintParser.Line> coveredOrder = BuildBlueprintParser.Ordered(covered);
		CheckText("telhado coberto: o piso 3 m acima vem ANTES do telhado", "wood_floor wood_roof_45",
			TracePrefabs(coveredOrder));

		// (b) no floor ABOVE the roof: the key falls back to the storey below. Alone it is still the one piece, and a
		// floor on the SAME level (dy = 0 is not "above") does not become the slab the roof covers.
		System.Collections.Generic.List<BuildBlueprintParser.Line> alone = Plan(Piece("wood_roof_45", 0f, 0f, 0f));
		CheckText("telhado sem piso acima: sozinho continua o telhado", "wood_roof_45",
			TracePrefabs(BuildBlueprintParser.Ordered(alone)));
		System.Collections.Generic.List<BuildBlueprintParser.Line> sameLevel = Plan(
			Piece("wood_roof_45", 0f, 0f, 0f),
			Piece("wood_floor", 0f, 0f, 0f));
		CheckText("telhado sem piso acima: o piso do MESMO nivel nao conta como laje coberta",
			"wood_floor wood_roof_45", TracePrefabs(BuildBlueprintParser.Ordered(sameLevel)));

		// (c) a tall ceiling (5 m) is still inside the window (0.3-6 m): the upper floor comes first.
		System.Collections.Generic.List<BuildBlueprintParser.Line> tall = Plan(
			Piece("wood_roof_45", 0f, 0f, 0f),
			Piece("wood_floor", 0f, 5f, 0f));
		CheckText("telhado coberto: piso a 5 m (pe-direito alto) tambem vem antes", "wood_floor wood_roof_45",
			TracePrefabs(BuildBlueprintParser.Ordered(tall)));

		// (d) out of the window (7 m): that floor is NOT the slab this roof covers, so it does not move the roof - the
		// roof keeps the storey below and is built before the floor far above it.
		System.Collections.Generic.List<BuildBlueprintParser.Line> far = Plan(
			Piece("wood_roof_45", 0f, 0f, 0f),
			Piece("wood_floor", 0f, 7f, 0f));
		CheckText("telhado sem piso na janela: piso a 7 m NAO conta (o telhado fica no andar de baixo)",
			"wood_roof_45 wood_floor", TracePrefabs(BuildBlueprintParser.Ordered(far)));

		// (d2) the window has a radius too (2.5 m in XZ): a slab 2.4 m to the side still covers the roof; one 3 m to
		// the side does not (the roof falls back to the storey below and is built first).
		System.Collections.Generic.List<BuildBlueprintParser.Line> nearSide = Plan(
			Piece("wood_roof_45", 0f, 0f, 0f),
			Piece("wood_floor", 2.4f, 3f, 0f));
		CheckText("telhado coberto: piso 2,4 m ao lado (dentro dos 2,5 m) conta como a laje", "wood_floor wood_roof_45",
			TracePrefabs(BuildBlueprintParser.Ordered(nearSide)));
		System.Collections.Generic.List<BuildBlueprintParser.Line> farSide = Plan(
			Piece("wood_roof_45", 0f, 0f, 0f),
			Piece("wood_floor", 3f, 3f, 0f));
		CheckText("telhado sem piso na janela: piso 3 m ao lado (fora dos 2,5 m) NAO conta",
			"wood_roof_45 wood_floor", TracePrefabs(BuildBlueprintParser.Ordered(farSide)));

		// (d3) the slab is the NEAREST floor above, not the highest one: with floors at 3 m and 5 m the roof goes
		// between them (the 3 m one covers it, the 5 m one belongs to the storey above).
		System.Collections.Generic.List<BuildBlueprintParser.Line> twoSlabs = Plan(
			Piece("wood_roof_45", 0f, 0f, 0f),
			Piece("wood_floor", 0f, 5f, 0f),
			Piece("wood_floor", 0f, 3f, 0f));
		CheckText("telhado coberto: a laje e o piso MAIS PROXIMO acima (3 m), nao o mais alto (5 m)",
			"wood_floor wood_roof_45 wood_floor", TracePrefabs(BuildBlueprintParser.Ordered(twoSlabs)));
		CheckText("telhado coberto: alturas na ordem (piso 3, telhado 0, piso 5)", "3 0 5",
			TraceY(BuildBlueprintParser.Ordered(twoSlabs)));

		// (e) the key cannot push a door to the middle: two storeys (floors at 0 and 3, the roof at 0 covering the slab
		// at 3) with the door written in the middle of the file - the door is still the LAST piece of the whole build.
		System.Collections.Generic.List<BuildBlueprintParser.Line> roofDoor = Plan(
			Piece("wood_roof_45", 0f, 0f, 0f),
			Piece("wood_door", 4f, 0f, 0f),
			Piece("woodwall", 0f, 0f, 0f),
			Piece("wood_floor", 0f, 3f, 0f),
			Piece("wood_floor", 0f, 0f, 0f));
		System.Collections.Generic.List<BuildBlueprintParser.Line> roofDoorOrder = BuildBlueprintParser.Ordered(roofDoor);
		CheckText("porta com telhado: piso de baixo, parede, piso de cima, telhado e a PORTA",
			"wood_floor woodwall wood_floor wood_roof_45 wood_door", TracePrefabs(roofDoorOrder));
		CheckInt("porta com telhado: nada de outra classe depois da 1a porta", 0, NotDoorAfterFirstDoor(roofDoorOrder));

		// (f) one storey only (floor, wall and roof on the same floor, written in the worst order): the floor comes
		// before the wall and both before the roof - the covered-slab lookup changes nothing inside a storey.
		System.Collections.Generic.List<BuildBlueprintParser.Line> oneStorey = Plan(
			Piece("wood_roof_45", 0f, 0f, 0f),
			Piece("woodwall", 0f, 0f, 0f),
			Piece("wood_floor", 0f, 0f, 0f));
		CheckText("um andar so: piso -> parede -> telhado", "wood_floor woodwall wood_roof_45",
			TracePrefabs(BuildBlueprintParser.Ordered(oneStorey)));
	}

	/// <summary>
	/// Section (o): P2 of the multi-build (03/11/2026) - <see cref="NavTileMath.Axis"/> is one axis of the game's own
	/// <c>Pathfinding.GetTile</c> (decompiled 1.0.20, <c>:797</c>: <c>FloorToInt((point + m_tileSize/2) / m_tileSize)</c>),
	/// which is what the coalesced invalidation uses to find the tile a piece falls in. The seams are the cases that
	/// matter: the grid is shifted by half a tile, so tile 0 spans -16..+16 with a 32 m tile, -16 and +16 are 0 and 1,
	/// and a sign error there marks the wrong tile - a build 32 m away, and the floor the companion needs stays out of
	/// the map. The last two cases are the ones a piece lives in: a 2 m piece stays in one tile, a piece on a seam
	/// marks two (which is why the invalidation marks the four corners as well). Pure - no Unity, no world.
	/// </summary>
	private static void NavTiles()
	{
		CheckInt("Axis(0, 32) -> o centro do tile 0", 0, NavTileMath.Axis(0f, 32f));
		CheckInt("Axis(15.99, 32) -> ainda o tile 0 (a emenda e em 16)", 0, NavTileMath.Axis(15.99f, 32f));
		CheckInt("Axis(16, 32) -> o primeiro tile do lado positivo", 1, NavTileMath.Axis(16f, 32f));
		CheckInt("Axis(-15.99, 32) -> ainda o tile 0 (a emenda e em -16)", 0, NavTileMath.Axis(-15.99f, 32f));
		CheckInt("Axis(-16, 32) -> -16 ainda e o tile 0 (Floor, nao Round)", 0, NavTileMath.Axis(-16f, 32f));
		CheckInt("Axis(-16.01, 32) -> o primeiro tile do lado negativo", -1, NavTileMath.Axis(-16.01f, 32f));
		CheckInt("Axis(-48, 32) -> -48 cai no tile -1 (e nao em -2)", -1, NavTileMath.Axis(-48f, 32f));
		CheckInt("Axis(1000, 32) -> 31 (1016/32 = 31,75)", 31, NavTileMath.Axis(1000f, 32f));
		Check("Axis(1, 32) == Axis(5, 32) -> uma peca de 2 m fica num tile so",
			NavTileMath.Axis(1f, 32f) == NavTileMath.Axis(5f, 32f),
			"obtido " + NavTileMath.Axis(1f, 32f) + " / " + NavTileMath.Axis(5f, 32f));
		Check("Axis(15, 32) != Axis(17, 32) -> peca na emenda marca dois tiles",
			NavTileMath.Axis(15f, 32f) != NavTileMath.Axis(17f, 32f),
			"obtido " + NavTileMath.Axis(15f, 32f) + " / " + NavTileMath.Axis(17f, 32f));
	}

	/// <summary>
	/// P1 (04/11/2026): the <b>support gate</b> of the build - is the piece waiting for something the plan itself is still
	/// going to put up (<c>Dependency</c>: no attempt spent, never counted as skipped) or did it really fail (<c>Real</c>:
	/// one of the 3 attempts)? The table is <c>src/Companions/AI/BuildFailure.cs</c>; <c>no_ground</c> is the only reason
	/// that reads the placement probe's answer (<c>groundBelow</c>).
	/// </summary>
	private static void BuildFailures()
	{
		// the three labels of a dependency, with and without the probe's answer (it is ignored for them)
		FailureCase("no_support", false, BuildFailure.Kind.Dependency);
		FailureCase("no_support", true, BuildFailure.Kind.Dependency);
		FailureCase("no support", false, BuildFailure.Kind.Dependency);
		FailureCase("needs_station", false, BuildFailure.Kind.Dependency);
		// the case of the measured run (04/11/2026): a status in another case must not turn a dependency into a real failure
		FailureCase("NO_SUPPORT", false, BuildFailure.Kind.Dependency);
		// no_ground is the one that depends on the probe: a hit is the piece of the plan below, a miss is a hole/the sea
		FailureCase("no_ground", true, BuildFailure.Kind.Dependency);
		FailureCase("no_ground", false, BuildFailure.Kind.Real);
		FailureCase("NO_GROUND", true, BuildFailure.Kind.Dependency);
		// a real failure spends an attempt
		FailureCase("no progress", false, BuildFailure.Kind.Real);
		FailureCase("blocked_by_player", true, BuildFailure.Kind.Real);
		FailureCase("clipping", false, BuildFailure.Kind.Real);
		FailureCase("too_steep", true, BuildFailure.Kind.Real);
		FailureCase("can't reach it", false, BuildFailure.Kind.Real);
		FailureCase("took too long", false, BuildFailure.Kind.Real);
		FailureCase("verification: not_found", false, BuildFailure.Kind.Real);
		FailureCase("verification: fallen", true, BuildFailure.Kind.Real);
		// nobody named the failure: that is not something to wait for
		FailureCase("", false, BuildFailure.Kind.Real);
		FailureCase(null, false, BuildFailure.Kind.Real);
		// an unknown status is real too, even with ground under the pose
		FailureCase("no_site", true, BuildFailure.Kind.Real);
		// the convenience form used by the build gate
		Check("IsDependency(\"no_support\", false) -> true (gasta nenhuma tentativa)",
			BuildFailure.IsDependency("no_support", false), "obtido false");
		Check("IsDependency(\"clipping\", false) -> false (gasta uma tentativa)",
			!BuildFailure.IsDependency("clipping", false), "obtido true");
		Check("IsDependency(\"no_ground\", true) -> true (a peca de baixo ainda nao subiu)",
			BuildFailure.IsDependency("no_ground", true), "obtido false");
		Check("IsDependency(\"no_ground\", false) -> false (nao ha nada embaixo: o local falhou)",
			!BuildFailure.IsDependency("no_ground", false), "obtido true");
	}

	/// <summary>Section (p): one classification, with the probe's answer and what the gate expects.</summary>
	private static void FailureCase(string why, bool groundBelow, BuildFailure.Kind expected)
	{
		BuildFailure.Kind actual = BuildFailure.Classify(why, groundBelow);
		Check("Classify(" + Show(why) + ", groundBelow=" + (groundBelow ? "true" : "false") + ") -> " + expected,
			actual == expected, "obtido " + actual);
	}

	/// <summary>
	/// P1 (04/11/2026): <b>structure or furniture</b> - the piece's name decides how far it may be moved from the plan's pose
	/// when the spot refuses it (<c>too_steep</c>/<c>no_ground</c>): a structural piece (floor, wall, roof, beam, pole, stair,
	/// door) only up to 1 m (moved further it no longer lines up with the house), furniture (a chest, a bed, a fire pit, a
	/// workbench, a fermenter) up to the whole 5 m search radius (3 rings x 12 on the wide attempt). An unknown name is
	/// furniture; an empty/null one is structural (the safe side).
	/// </summary>
	private static void PieceKinds()
	{
		// the structure - every one of them only gets the small nudge
		KindCase("wood_floor", BuildPieceKind.Kind.Structural);
		KindCase("wood_foundation", BuildPieceKind.Kind.Structural);
		KindCase("woodwall", BuildPieceKind.Kind.Structural);
		KindCase("wood_wall_half", BuildPieceKind.Kind.Structural);
		KindCase("wood_roof_45", BuildPieceKind.Kind.Structural);
		KindCase("wood_wall_roof_top_45", BuildPieceKind.Kind.Structural);
		KindCase("wood_beam_45", BuildPieceKind.Kind.Structural);
		KindCase("wood_pole2", BuildPieceKind.Kind.Structural);
		KindCase("wood_stair", BuildPieceKind.Kind.Structural);
		KindCase("wood_stepladder", BuildPieceKind.Kind.Structural);
		KindCase("wood_door", BuildPieceKind.Kind.Structural);
		KindCase("wood_gate", BuildPieceKind.Kind.Structural);
		// the case of the measured run (04/11/2026): chests and a bed refused with too_steep and never adjusted
		KindCase("piece_chest_wood", BuildPieceKind.Kind.Furniture);
		KindCase("bed", BuildPieceKind.Kind.Furniture);
		KindCase("piece_bed02", BuildPieceKind.Kind.Furniture);
		KindCase("fire_pit", BuildPieceKind.Kind.Furniture);
		KindCase("piece_workbench", BuildPieceKind.Kind.Furniture);
		KindCase("fermenter", BuildPieceKind.Kind.Furniture);
		KindCase("piece_cauldron", BuildPieceKind.Kind.Furniture);
		// case does not matter (the prefab names come from a file somebody typed)
		KindCase("WOOD_FLOOR", BuildPieceKind.Kind.Structural);
		KindCase("Piece_Chest_Wood", BuildPieceKind.Kind.Furniture);
		// nobody said what it is: the safe side (never move it); a name the vocabulary does not know is furniture (the piece
		// still gets placed, and "furniture = everything that is not structure" is the rule)
		KindCase("", BuildPieceKind.Kind.Structural);
		KindCase(null, BuildPieceKind.Kind.Structural);
		KindCase("xyz", BuildPieceKind.Kind.Furniture);
		// the convenience form the nudge uses
		Check("IsStructural(\"woodwall\") -> true (so 1 m no maximo)", BuildPieceKind.IsStructural("woodwall"),
			"obtido false");
		Check("IsStructural(\"piece_chest_wood\") -> false (vai ate 5 m)",
			!BuildPieceKind.IsStructural("piece_chest_wood"), "obtido true");
	}

	/// <summary>Section (q): one piece name and the kind the nudge expects for it.</summary>
	private static void KindCase(string prefab, BuildPieceKind.Kind expected)
	{
		BuildPieceKind.Kind actual = BuildPieceKind.Classify(prefab);
		Check("Classify(" + Show(prefab) + ") -> " + expected, actual == expected, "obtido " + actual);
	}

	/// <summary>
	/// P1 (04/11/2026): the <b>direct support</b> of a piece - the decisions <c>MHR_Build.PullDirectSupport</c> makes when a
	/// piece of a group's plan is refused with <c>no_support</c>: which plan piece (0,4 to 2,5 m <b>below</b>, up to 2,5 m in
	/// XZ) is what holds it up (<c>IsDirectSupport</c>/<c>Rank</c>), and which CLASSES can hold what (<c>CanSupport</c>: a
	/// door never, a roof only a roof - the row below the slope of a gable). The measured case: <c>wood_floor</c>/<c>
	/// wood_roof_45</c> at 43,8/45,8/46,8 waiting for the floor/beam at 42,8 that was still ahead in the plan, and the east
	/// gable that stayed open because every roof was refused as a support.
	/// </summary>
	private static void SupportBelow()
	{
		// the measured house: the piece 1 m below it, right under it (43,8 -> 42,8 in the log of 04/11/2026)
		SupportCase(0f, 0f, 1f, true, "1 m abaixo, embaixo dela (o caso medido)");
		// the same storey is a NEIGHBOUR, not a support - and this is what keeps the pull descending
		SupportCase(0f, 0f, 0f, false, "mesmo andar: vizinha, nao apoio");
		SupportCase(0f, 0f, -1f, false, "acima dela: o apoio nunca vem de cima");
		// the drop window
		SupportCase(0f, 0f, 0.39f, false, "logo abaixo, mas menos de 0,4 m: ainda e o mesmo andar");
		SupportCase(0f, 0f, 0.4f, true, "borda de baixo da janela");
		SupportCase(0f, 0f, 2.5f, true, "borda de cima da janela (parede/telhado de 2 m)");
		SupportCase(0f, 0f, 2.6f, false, "abaixo demais: nao e o que segura a peca");
		// the flat window
		SupportCase(2.5f, 0f, 1f, true, "2,5 m ao lado");
		SupportCase(2.6f, 0f, 1f, false, "2,6 m ao lado: fora");
		SupportCase(1.7f, 1.7f, 1f, true, "diagonal de 2,40 m");
		SupportCase(1.8f, 1.8f, 1f, false, "diagonal de 2,55 m: fora");
		SupportCase(-2f, -1f, 1f, true, "para o outro lado, 2,24 m");
		// the ranking: what is directly under it beats what is nearer but lower
		Check("Rank(0,0,1) == 10 (a queda pesa 10x a distancia)", Math.Abs(BuildSupportBelow.Rank(0f, 0f, 1f) - 10f) < 0.001f,
			"obtido " + BuildSupportBelow.Rank(0f, 0f, 1f).ToString("F2"));
		Check("Rank(2,0,1) == 12", Math.Abs(BuildSupportBelow.Rank(2f, 0f, 1f) - 12f) < 0.001f,
			"obtido " + BuildSupportBelow.Rank(2f, 0f, 1f).ToString("F2"));
		Check("Rank(2.4,0,0.5) < Rank(0,0,2.0): o de 0,5 m abaixo ganha do de 2 m",
			BuildSupportBelow.Rank(2.4f, 0f, 0.5f) < BuildSupportBelow.Rank(0f, 0f, 2f),
			"obtido " + BuildSupportBelow.Rank(2.4f, 0f, 0.5f).ToString("F2") + " x "
				+ BuildSupportBelow.Rank(0f, 0f, 2f).ToString("F2"));
		Check("Rank(0,0,1) < Rank(2,0,1): com a mesma queda, o mais perto ganha",
			BuildSupportBelow.Rank(0f, 0f, 1f) < BuildSupportBelow.Rank(2f, 0f, 1f), "obtido a ordem invertida");
		// the numbers themselves, so a silent change of the window is caught here
		Check("MaxFlat == 2,5 m", Math.Abs(BuildSupportBelow.MaxFlat - 2.5f) < 0.001f,
			"obtido " + BuildSupportBelow.MaxFlat.ToString("F2"));
		Check("MaxDrop == 2,5 m", Math.Abs(BuildSupportBelow.MaxDrop - 2.5f) < 0.001f,
			"obtido " + BuildSupportBelow.MaxDrop.ToString("F2"));
		Check("MinDrop == 0,4 m", Math.Abs(BuildSupportBelow.MinDrop - 0.4f) < 0.001f,
			"obtido " + BuildSupportBelow.MinDrop.ToString("F2"));
		// the CLASS rule (04/11/2026, the east gable of the deck run): a roof is held by another roof, never the other way
		CanSupportCase(3, 3, true, "telhado sobre telhado: a fileira de baixo da agua (o caso medido)");
		CanSupportCase(3, 0, true, "telhado apoiado no piso 2 m abaixo (medido: roof 46,8 / floor 44,8)");
		CanSupportCase(3, 2, true, "telhado no topo da parede/viga");
		CanSupportCase(3, 1, true, "telhado no topo da escada");
		CanSupportCase(0, 3, false, "piso nao e apoiado por telhado");
		CanSupportCase(2, 3, false, "parede nao e apoiada por telhado");
		CanSupportCase(3, 4, false, "telhado nao e apoiado por porta");
		CanSupportCase(0, 4, false, "porta nunca apoia nada (piso)");
		CanSupportCase(0, 0, true, "piso sobre piso");
		CanSupportCase(4, 2, true, "a porta e a PECA: pode ser apoiada por uma parede");
		// the POST of the emergency support (04/11/2026, the deck run: a fixed 2 m beam under a 1 m gap was buried and the
		// game refused the post itself). The lengths are the measured ones, shortest first: beam 1 m, beam 2 m, pole 2 m,
		// beam 45 (2*sqrt2)
		float[] posts = { 1f, 2f, 2f, 2.83f };
		PostCase(posts, 1.02f, 0, "o caso medido: peca ~1 m acima do chao -> viga de 1 m");
		PostCase(posts, 1.4f, -1, "vão de 1,4 m: nenhum poste alcanca (1 m fica curto, 2 m passa demais)");
		PostCase(posts, 1.9f, 1, "-> viga de 2 m (o empate com a ponta de 2 m fica com a primeira)");
		PostCase(posts, 2.2f, 1, "2 m ainda alcanca (borda)");
		PostCase(posts, 2.5f, 3, "2 m fica curto, 2,83 m alcanca");
		PostCase(posts, 2.87f, 3, "o segundo degrau medido (piso de 42,8 sobre o de 39,8)");
		PostCase(posts, 3.2f, -1, "vão de 3,2 m: nem a viga de 45 alcanca");
		PostCase(posts, 0.5f, -1, "vão de 0,5 m: 1 m passaria 0,5 m atraves da peca");
		PostCase(new[] { 0f, 2f }, 2f, 1, "prefab que o jogo nao tem (comprimento 0) e pulado");
		PostCase(new[] { 2.83f, 1f, 2f }, 1f, 1, "a escolha e pelo COMPRIMENTO, nao pela ordem da lista");
		PostCase(null, 1f, -1, "sem lista, sem poste");
		Check("ShortTolerance == 0,20 m", Math.Abs(BuildSupportBelow.ShortTolerance - 0.2f) < 0.001f,
			"obtido " + BuildSupportBelow.ShortTolerance.ToString("F2"));
		Check("MaxOverlap == 0,35 m", Math.Abs(BuildSupportBelow.MaxOverlap - 0.35f) < 0.001f,
			"obtido " + BuildSupportBelow.MaxOverlap.ToString("F2"));
	}

	/// <summary>Section (r): one candidate of the direct-support window.</summary>
	private static void SupportCase(float dx, float dz, float drop, bool expected, string why)
	{
		bool actual = BuildSupportBelow.IsDirectSupport(dx, dz, drop);
		Check("IsDirectSupport(" + dx.ToString("F1") + ", " + dz.ToString("F1") + ", " + drop.ToString("F2") + ") -> "
			+ (expected ? "true" : "false") + " (" + why + ")", actual == expected,
			"obtido " + (actual ? "true" : "false"));
	}

	/// <summary>Section (r): one (piece class, candidate class) pair of the support rule.</summary>
	private static void CanSupportCase(int piece, int candidate, bool expected, string why)
	{
		bool actual = BuildSupportBelow.CanSupport(piece, candidate);
		Check("CanSupport(peca " + piece + ", apoio " + candidate + ") -> " + (expected ? "true" : "false") + " (" + why + ")",
			actual == expected, "obtido " + (actual ? "true" : "false"));
	}

	/// <summary>Section (r): one gap and the post the emergency support picks for it.</summary>
	private static void PostCase(float[] lengths, float gap, int expected, string why)
	{
		int actual = BuildSupportBelow.PickPost(lengths, gap);
		Check("PickPost(vão " + gap.ToString("F2") + " m) -> " + expected + " (" + why + ")",
			actual == expected, "obtido " + actual);
	}

	/// <summary>
	/// P1 (04/11/2026): the <b>body window</b> of the placement check (<c>BuildProbe.BlockedByCharacter</c>). The measured
	/// bug: with the 1 m deck, a floor 1 m above the feet of two companions was 1,0 m from the piece's point in 3D, so the
	/// old point-vs-transform test (0,6 m) let it through and the floor went <b>over their heads</b> (squeezed to y 37,9, 1 m
	/// under the ground). The body is a column and the piece a box: both windows have to hold.
	/// </summary>
	private static void BodyWindows()
	{
		// the measured floor (wood_floor: 2,27 x 0,26 x 2,29 -> flat radius 1,14, half height 0,13) at the deck level 39,8
		// over a companion standing on the ground at 38,78: the floor crosses the body -> blocked
		float floor = BuildBody.Spacing(1.14f, 0.6f);
		Check("o caso medido: piso do deck (39,8) sobre o companheiro (pes 38,78) -> bloqueia",
			BuildBody.Blocked(0f, 38.78f, 0f, 0f, 39.8f, 0f, 0.13f, floor), "obtido false");
		// the same floor beside the body (1,7 m) -> free
		Check("o mesmo piso a 1,7 m do companheiro -> livre",
			!BuildBody.Blocked(1.7f, 38.78f, 0f, 0f, 39.8f, 0f, 0.13f, floor), "obtido true");
		// 1,5 m is inside the 1,59 m window (piece radius + body radius)
		Check("o mesmo piso a 1,5 m -> ainda bloqueia (janela de 1,59 m)",
			BuildBody.Blocked(1.5f, 38.78f, 0f, 0f, 39.8f, 0f, 0.13f, floor), "obtido false");
		// a floor above the head: the body ends at 40,78 and the piece starts at 41,67 -> free
		Check("piso 2 m acima da cabeca -> livre",
			!BuildBody.Blocked(0f, 38.78f, 0f, 0f, 41.8f, 0f, 0.13f, floor), "obtido true");
		// a floor at the feet (the piece under the companion)
		Check("piso no nivel dos pes -> bloqueia",
			BuildBody.Blocked(0f, 38.78f, 0f, 0f, 38.78f, 0f, 0.13f, floor), "obtido false");
		// a wall (2,25 x 2,16 x 0,53 -> flat 1,125, half height 1,08): its base at 38,72 crosses the body -> blocked
		float wall = BuildBody.Spacing(1.125f, 0.6f);
		Check("parede de 2 m com a base na altura do corpo -> bloqueia",
			BuildBody.Blocked(0f, 38.78f, 0f, 0f, 39.8f, 0f, 1.08f, wall), "obtido false");
		// the same wall one metre higher: base 40,92, body ends at 40,78 -> free (the companion walks under it)
		Check("parede inteira acima da cabeca -> livre",
			!BuildBody.Blocked(0f, 38.78f, 0f, 0f, 42f, 0f, 1.08f, wall), "obtido true");
		// a companion standing ON the deck (39,93): the neighbouring tile is 2 m away -> free; on the seam (1,4 m) -> blocked
		Check("companheiro de pe no deck: a telha vizinha (2 m) -> livre",
			!BuildBody.Blocked(2f, 39.93f, 0f, 0f, 39.8f, 0f, 0.13f, floor), "obtido true");
		Check("companheiro na emenda das telhas (1,4 m) -> bloqueia",
			BuildBody.Blocked(1.4f, 39.93f, 0f, 0f, 39.8f, 0f, 0.13f, floor), "obtido false");
		// the spacing never goes under the probe's MinSpacing, and it grows with the piece
		Check("Spacing(0, 0,6) == 0,60", Math.Abs(BuildBody.Spacing(0f, 0.6f) - 0.6f) < 0.001f,
			"obtido " + BuildBody.Spacing(0f, 0.6f).ToString("F2"));
		Check("Spacing(0,1, 0,6) == 0,60", Math.Abs(BuildBody.Spacing(0.1f, 0.6f) - 0.6f) < 0.001f,
			"obtido " + BuildBody.Spacing(0.1f, 0.6f).ToString("F2"));
		Check("Spacing(1,14, 0,6) == 1,59 (raio do piso + 0,45)", Math.Abs(BuildBody.Spacing(1.14f, 0.6f) - 1.59f) < 0.001f,
			"obtido " + BuildBody.Spacing(1.14f, 0.6f).ToString("F2"));
		// the numbers of the window
		Check("Radius == 0,45 m", Math.Abs(BuildBody.Radius - 0.45f) < 0.001f, "obtido " + BuildBody.Radius.ToString("F2"));
		Check("Below == 0,50 m", Math.Abs(BuildBody.Below - 0.5f) < 0.001f, "obtido " + BuildBody.Below.ToString("F2"));
		Check("Above == 2,00 m", Math.Abs(BuildBody.Above - 2f) < 0.001f, "obtido " + BuildBody.Above.ToString("F2"));
	}

	/// <summary>The plan of case (a)/(e) of section (m): a fresh list every call (case (e) needs two of them).</summary>
	private static System.Collections.Generic.List<BuildBlueprintParser.Line> ZigzagPlan()
	{
		return Plan(
			Piece("wood_floor", 0f, 0f, 0f),
			Piece("wood_floor", 10f, 0f, 0f),
			Piece("wood_floor", 0f, 0f, 1f),
			Piece("wood_floor", 10f, 0f, 1f));
	}

	private static BuildBlueprintParser.Line Piece(string prefab, float px, float py, float pz)
	{
		return new BuildBlueprintParser.Line { Prefab = prefab, Px = px, Py = py, Pz = pz };
	}

	private static System.Collections.Generic.List<BuildBlueprintParser.Line> Plan(
		params BuildBlueprintParser.Line[] pieces)
	{
		return new System.Collections.Generic.List<BuildBlueprintParser.Line>(pieces);
	}

	/// <summary>E7: "x,z" of the ordered plan - the path, without the heights.</summary>
	private static string TraceXZ(System.Collections.Generic.List<BuildBlueprintParser.Line> ordered)
	{
		StringBuilder text = new StringBuilder();
		for (int i = 0; i < ordered.Count; i++)
		{
			if (i > 0)
			{
				text.Append(' ');
			}
			text.Append(Number(ordered[i].Px)).Append(',').Append(Number(ordered[i].Pz));
		}
		return text.ToString();
	}

	private static string TraceY(System.Collections.Generic.List<BuildBlueprintParser.Line> ordered)
	{
		StringBuilder text = new StringBuilder();
		for (int i = 0; i < ordered.Count; i++)
		{
			if (i > 0)
			{
				text.Append(' ');
			}
			text.Append(Number(ordered[i].Py));
		}
		return text.ToString();
	}

	private static string TracePrefabs(System.Collections.Generic.List<BuildBlueprintParser.Line> ordered)
	{
		StringBuilder text = new StringBuilder();
		for (int i = 0; i < ordered.Count; i++)
		{
			if (i > 0)
			{
				text.Append(' ');
			}
			text.Append(ordered[i].Prefab);
		}
		return text.ToString();
	}

	/// <summary>Section (m): the ordered list is a permutation of the input (by reference - no piece is created).</summary>
	private static bool SamePieces(System.Collections.Generic.List<BuildBlueprintParser.Line> input,
		System.Collections.Generic.List<BuildBlueprintParser.Line> output)
	{
		if (input == null || output == null || input.Count != output.Count)
		{
			return false;
		}
		System.Collections.Generic.List<BuildBlueprintParser.Line> pool =
			new System.Collections.Generic.List<BuildBlueprintParser.Line>(input);
		for (int i = 0; i < output.Count; i++)
		{
			int at = pool.IndexOf(output[i]);
			if (at < 0)
			{
				return false;
			}
			pool.RemoveAt(at);
		}
		return true;
	}

	private static bool SameOrder(System.Collections.Generic.List<BuildBlueprintParser.Line> a,
		System.Collections.Generic.List<BuildBlueprintParser.Line> b)
	{
		if (a == null || b == null || a.Count != b.Count)
		{
			return false;
		}
		for (int i = 0; i < a.Count; i++)
		{
			if (!ReferenceEquals(a[i], b[i]))
			{
				return false;
			}
		}
		return true;
	}

	/// <summary>Section (m): the length of the walk the companion does (sum of the XZ steps, in metres).</summary>
	private static float PathLength(System.Collections.Generic.List<BuildBlueprintParser.Line> ordered)
	{
		float total = 0f;
		for (int i = 1; i < ordered.Count; i++)
		{
			float dx = ordered[i].Px - ordered[i - 1].Px;
			float dz = ordered[i].Pz - ordered[i - 1].Pz;
			total += (float)Math.Sqrt(dx * dx + dz * dz);
		}
		return total;
	}

	/// <summary>Section (m): how many consecutive steps are longer than <paramref name="minDistance"/> - the zigzag
	/// crosses the build at every step, the path crosses it once.</summary>
	private static int LongJumps(System.Collections.Generic.List<BuildBlueprintParser.Line> ordered, float minDistance)
	{
		int jumps = 0;
		for (int i = 1; i < ordered.Count; i++)
		{
			float dx = ordered[i].Px - ordered[i - 1].Px;
			float dz = ordered[i].Pz - ordered[i - 1].Pz;
			if (Math.Sqrt(dx * dx + dz * dz) > minDistance)
			{
				jumps++;
			}
		}
		return jumps;
	}

	private static int NotDoorAfterFirstDoor(System.Collections.Generic.List<BuildBlueprintParser.Line> ordered)
	{
		int firstDoor = -1;
		for (int i = 0; i < ordered.Count; i++)
		{
			if (BuildBlueprintParser.ClassOf(ordered[i].Prefab) == 4)
			{
				firstDoor = i;
				break;
			}
		}
		if (firstDoor < 0)
		{
			return ordered.Count;      // no door at all: the case that wanted one fails, as it should
		}
		int others = 0;
		for (int i = firstDoor + 1; i < ordered.Count; i++)
		{
			if (BuildBlueprintParser.ClassOf(ordered[i].Prefab) != 4)
			{
				others++;
			}
		}
		return others;
	}

	private static int IndexOfPrefab(System.Collections.Generic.List<BuildBlueprintParser.Line> ordered, string prefab)
	{
		for (int i = 0; i < ordered.Count; i++)
		{
			if (string.Equals(ordered[i].Prefab, prefab, StringComparison.Ordinal))
			{
				return i;
			}
		}
		return -1;
	}

	/// <summary>Section (m): a float without the culture (the trace text is compared with CheckText).</summary>
	private static string Number(float value)
	{
		return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
	}

	private static void Templates()
	{
		// the names, in the five languages
		TemplateCase("constrói uma cabana aqui", "hut");
		TemplateCase("constrói um abrigo aqui", "house");
		TemplateCase("construa uma casa aqui", "house");
		TemplateCase("constrói um abrigo para a bancada", "canopy");
		TemplateCase("abrigo da bancada", "canopy");
		TemplateCase("build a small hut", "hut");
		TemplateCase("build a small house", "house");
		TemplateCase("a shelter for the bench", "canopy");
		TemplateCase("construye una choza", "hut");
		TemplateCase("construye una casita", "house");
		TemplateCase("un cobertizo", "canopy");
		TemplateCase("construis une cabane", "hut");
		TemplateCase("construis une petite maison", "house");
		TemplateCase("un auvent", "canopy");
		TemplateCase("costruisci una casetta", "hut");
		TemplateCase("costruisci una casa piccola", "house");
		TemplateCase("un riparo", "canopy");
		// E6c (02/11/2026): as casas 6x6 e 4x4, a tenda e o "barraco" (a cabana 2x2)
		TemplateCase("construa uma casa 6x6 aqui", "house6");
		TemplateCase("constrói uma casa média aqui", "house6");
		TemplateCase("build a 6x6 house", "house6");
		TemplateCase("construye una casa 6x6", "house6");
		TemplateCase("construis une maison 6x6", "house6");
		TemplateCase("costruisci una casa 6x6", "house6");
		TemplateCase("construa uma casa 4x4 aqui", "house4");
		TemplateCase("constrói uma casa pequena", "house4");
		TemplateCase("build a 4x4 house", "house4");
		TemplateCase("construye una casita 4x4", "house4");
		TemplateCase("construis une petite maison 4x4", "house4");
		TemplateCase("constrói uma tenda aqui", "tent");
		TemplateCase("monta um toldo aqui", "tent");
		TemplateCase("build a tent", "tent");
		TemplateCase("construye una tienda de campaña", "tent");
		TemplateCase("construis une tente", "tent");
		TemplateCase("costruisci una tenda", "tent");
		TemplateCase("constrói um barraco aqui", "hut");
		TemplateCase("build a shack", "hut");
		Check("Resolve(\"constrói uma parede\") == null (template nao e peca)", BuildTemplates.Resolve("constrói uma parede") == null);
		Check("Resolve(null) == null", BuildTemplates.Resolve(null) == null);
		// "cabana" nao pode ser lida como a peca "cama" (o catalogo da E5 nao conhece cabana)
		Check("BuildCatalog.Resolve(\"cabana\") == null (nao e peca)", BuildCatalog.Resolve("uma cabana") == null);
		Check("BuildCatalog.Resolve(\"cama\") == \"bed\"", BuildCatalog.Resolve("uma cama") == "bed");

		BuildTemplates.Template hut = BuildTemplates.ByName("hut");
		BuildTemplates.Template canopy = BuildTemplates.ByName("canopy");
		BuildTemplates.Template house = BuildTemplates.ByName("house");
		Check("ByName(\"hut\")", hut != null);
		Check("ByName(\"canopy\")", canopy != null);
		Check("ByName(\"house\")", house != null);
		Check("ByName(\"xyz\") == null", BuildTemplates.ByName("xyz") == null);

		// a casa simples: 8x8 (16 pisos), 12 paredes, 1 porta, 1 janela (meia-parede), 16 telhados e 8 empenas
		CheckInt("casa: 57 pecas (com a bancada)", 57, house.Steps.Length);
		CheckInt("casa: 16 pisos", 16, CountPrefab(house, "wood_floor"));
		CheckInt("casa: 14 paredes (16 tiles - porta - janela)", 14, CountPrefab(house, "woodwall"));
		CheckInt("casa: 1 porta", 1, CountPrefab(house, "wood_door"));
		CheckInt("casa: 1 janela (meia-parede)", 1, CountPrefab(house, "wood_wall_half"));
		CheckInt("casa: 16 telhados", 16, CountPrefab(house, "wood_roof_45"));
		CheckInt("casa: 8 empenas", 8, CountPrefab(house, "wood_wall_roof_45"));
		CheckText("casa: a 1a peca e a BANCADA (os pisos precisam dela)", "piece_workbench", house.Ordered()[0].Prefab);
		CheckInt("casa: a bancada e nivel 0", 0, house.Ordered()[0].Level);
		CheckText("casa: a ultima peca e a porta", "wood_door", house.Ordered()[house.Steps.Length - 1].Prefab);
		CheckInt("casa: a porta e a ULTIMA (nivel 3 - senao ele se tranca dentro)", 3, LevelOf(house, "wood_door"));
		CheckInt("casa: a janela esta no nivel 1 (parede)", 1, LevelOf(house, "wood_wall_half"));
		// a frente e o lado -z: a porta e a janela estao em z = -4 (o lado que o jogador encara)
		CheckFloat("casa: a porta esta na frente (z = -4)", -4f, ZOf(house, "wood_door"));
		CheckFloat("casa: a janela esta na frente (z = -4)", -4f, ZOf(house, "wood_wall_half"));
		CheckFloat("casa: a janela tem peitoril de 0,5 m", 0.5f, YOf(house, "wood_wall_half"));

		// a composicao da cabana: 1 piso + 4 paredes + 2 telhados (o aceite da E6)
		CheckInt("cabana: 7 pecas", 7, hut.Steps.Length);
		CheckInt("cabana: 1 piso", 1, CountPrefab(hut, "wood_floor"));
		CheckInt("cabana: 3 paredes + 1 porta (E6d)", 3, CountPrefab(hut, "woodwall"));
		CheckInt("cabana: 1 porta", 1, CountPrefab(hut, "wood_door"));
		CheckInt("cabana: 2 telhados", 2, CountPrefab(hut, "wood_roof_45"));
		CheckInt("abrigo: 8 pecas", 8, canopy.Steps.Length);
		CheckInt("abrigo: 4 postes", 4, CountPrefab(canopy, "wood_pole2"));
		CheckInt("abrigo: 4 telhados", 4, CountPrefab(canopy, "wood_roof_45"));

		// E6c: a casa 6x6 (3x3 tiles) - 2 pecas de telhado por agua + 4 postes do beiral + a bancada
		BuildTemplates.Template house6 = BuildTemplates.ByName("house6");
		BuildTemplates.Template house4 = BuildTemplates.ByName("house4");
		BuildTemplates.Template tent = BuildTemplates.ByName("tent");
		Check("ByName(\"house6\")", house6 != null);
		Check("ByName(\"house4\")", house4 != null);
		CheckInt("casa 6x6: 9 pisos", 9, CountPrefab(house6, "wood_floor"));
		CheckInt("casa 6x6: 10 paredes (12 tiles - porta - janela)", 10, CountPrefab(house6, "woodwall"));
		CheckInt("casa 6x6: 1 porta", 1, CountPrefab(house6, "wood_door"));
		CheckInt("casa 6x6: 1 janela", 1, CountPrefab(house6, "wood_wall_half"));
		CheckInt("casa 6x6: 12 telhados (3 colunas x 2 aguas x 2 pecas)", 12, CountPrefab(house6, "wood_roof_45"));
		CheckInt("casa 6x6: 8 empenas", 8, CountPrefab(house6, "wood_wall_roof_45"));
		CheckInt("casa 6x6: 4 postes do beiral", 4, CountPrefab(house6, "wood_pole2"));
		CheckInt("casa 6x6: 46 pecas", 46, house6.Steps.Length);
		CheckText("casa 6x6: a porta e a ULTIMA", "wood_door", house6.Ordered()[house6.Steps.Length - 1].Prefab);
		CheckFloat("casa 6x6: a parede da frente em z = -3", -3f, ZOf(house6, "wood_door"));
		CheckInt("casa 4x4: 4 pisos", 4, CountPrefab(house4, "wood_floor"));
		CheckInt("casa 4x4: 4 telhados (2 colunas x 2 aguas x 1 peca)", 4, CountPrefab(house4, "wood_roof_45"));
		CheckInt("casa 4x4: 21 pecas (a bancada vai no anexo, fora)", 21, house4.Steps.Length);
		CheckInt("casa 4x4: 1 bancada (anexo fora)", 1, CountPrefab(house4, "piece_workbench"));
		CheckText("casa 4x4: a bancada e a 1a peca (os pisos precisam dela)", "piece_workbench", house4.Ordered()[0].Prefab);
		CheckText("casa 4x4: a porta e a ULTIMA", "wood_door", house4.Ordered()[house4.Steps.Length - 1].Prefab);
		CheckInt("tenda: 4 pecas (2 postes + 2 telhados)", 4, tent.Steps.Length);
		CheckInt("tenda: 2 postes", 2, CountPrefab(tent, "wood_pole2"));
		CheckInt("tenda: 2 telhados", 2, CountPrefab(tent, "wood_roof_45"));
		CheckInt("tenda: nenhum piso (aberta)", 0, CountPrefab(tent, "wood_floor"));

		// a ordem de construcao: fundacao -> parede -> telhado
		System.Collections.Generic.List<BuildTemplates.Step> ordered = hut.Ordered();
		CheckText("cabana: a 1a peca e o piso", "wood_floor", ordered[0].Prefab);
		CheckInt("cabana: o piso e nivel 0", 0, ordered[0].Level);
		CheckText("cabana: a ultima peca e a PORTA (E6d - e a ultima, senao ele se tranca)", "wood_door", ordered[ordered.Count - 1].Prefab);
		CheckInt("cabana: a porta e nivel 3", 3, ordered[ordered.Count - 1].Level);
		int lastLevel = -1;
		bool sorted = true;
		foreach (BuildTemplates.Step step in ordered)
		{
			if (step.Level < lastLevel)
			{
				sorted = false;
			}
			lastLevel = step.Level;
		}
		Check("cabana: Ordered() nunca volta de nivel", sorted);
		CheckInt("abrigo: os 4 primeiros sao os postes (nivel 0)", 0, canopy.Ordered()[3].Level);
		CheckInt("abrigo: o 5o ja e telhado (nivel 2)", 2, canopy.Ordered()[4].Level);

		// os deslocamentos medidos (snaps): piso no centro, parede 1 m acima, telhado 3 m acima
		CheckFloat("piso em (0,0,0)", 0f, ordered[0].X);
		CheckFloat("parede a 1 m", 1f, ordered[1].Y);
		BuildTemplates.Step hutRoof = ordered.Find(delegate (BuildTemplates.Step step) { return step.Prefab == "wood_roof_45"; });
		CheckFloat("telhado a 2 m (a agua encosta no topo da parede)", 2f, hutRoof.Y);
		CheckFloat("telhado com 1 m de beiral", 1f, Math.Abs(hutRoof.Z));

		// a allow-list dos planos
		foreach (string prefab in BuildTemplates.Prefabs())
		{
			Check("Allowed(\"" + prefab + "\")", BuildTemplates.Allowed(prefab));
		}
		Check("Allowed(\"piece_workbench\") (a casa traz a bancada)", BuildTemplates.Allowed("piece_workbench"));
		Check("Allowed(\"wood_beam_45\") == false (nao e peca de template)", !BuildTemplates.Allowed("wood_beam_45"));
		Check("Allowed(null) == false", !BuildTemplates.Allowed(null));
		CheckInt("Prefabs() sem repeticao", 8, BuildTemplates.Prefabs().Length);
	}

	/// <summary>E6: the level of the first step of a prefab (0 foundation, 1 walls, 2 roof).</summary>
	private static int LevelOf(BuildTemplates.Template template, string prefab)
	{
		foreach (BuildTemplates.Step step in template.Steps)
		{
			if (step.Prefab == prefab)
			{
				return step.Level;
			}
		}
		return -1;
	}

	/// <summary>E6: the z offset of the first step of a prefab.</summary>
	private static float ZOf(BuildTemplates.Template template, string prefab)
	{
		foreach (BuildTemplates.Step step in template.Steps)
		{
			if (step.Prefab == prefab)
			{
				return step.Z;
			}
		}
		return 999f;
	}

	/// <summary>E6: the y offset of the first step of a prefab.</summary>
	private static float YOf(BuildTemplates.Template template, string prefab)
	{
		foreach (BuildTemplates.Step step in template.Steps)
		{
			if (step.Prefab == prefab)
			{
				return step.Y;
			}
		}
		return 999f;
	}

	private static int CountPrefab(BuildTemplates.Template template, string prefab)
	{
		int count = 0;
		foreach (BuildTemplates.Step step in template.Steps)
		{
			if (step.Prefab == prefab)
			{
				count++;
			}
		}
		return count;
	}

	private static void TemplateCase(string text, string expected)
	{
		BuildTemplates.Template template = BuildTemplates.Resolve(text);
		CheckText("Template(" + Show(text) + ")", expected, template != null ? template.Name : null);
	}

	private static void BuildCase(string text, string expected)
	{
		CheckText("Resolve(" + Show(text) + ")", expected, BuildCatalog.Resolve(text));
	}

	private static void Names()
	{
		string[] roster = new string[] { "Gruncha", "Gruncho" };
		CheckText("InMessage(\"Gruncha, colete 10 pedras\")", "Gruncha",
			CompanionNameMatch.InMessage("Gruncha, colete 10 pedras", roster));
		CheckText("InMessage(\"gruncha colete 10 pedras\") sem caixa", "Gruncha",
			CompanionNameMatch.InMessage("gruncha colete 10 pedras", roster));
		CheckText("InMessage(\"Ei, Gruncha, vem cá\")", "Gruncha",
			CompanionNameMatch.InMessage("Ei, Gruncha, vem cá", roster));
		Check("InMessage(\"colete 10 pedras perto do Gruncha\") == null (no meio nao e chamado)",
			CompanionNameMatch.InMessage("colete 10 pedras perto do Gruncha", roster) == null);
		Check("InMessage(..., [\"Lu\"]) == null (nome curto demais na mensagem)",
			CompanionNameMatch.InMessage("Lu, colete 10 pedras", new string[] { "Lu" }) == null);
		Check("Same(\"o Gruncha\", \"Gruncha\") (artigo)", CompanionNameMatch.Same("o Gruncha", "Gruncha"));
		Check("Same(\"Grüncha\", \"gruncha\") (acento)", CompanionNameMatch.Same("Grüncha", "gruncha"));
		CheckText("AddressCandidate(\"Fulano, colete 10 pedras\")", "Fulano",
			CompanionNameMatch.AddressCandidate("Fulano, colete 10 pedras"));
		Check("AddressCandidate(\"mano, colete 10 pedras\") == null (forma de tratamento)",
			CompanionNameMatch.AddressCandidate("mano, colete 10 pedras") == null);
		Check("AddressCandidate(\"bom dia, tudo bem?\") == null",
			CompanionNameMatch.AddressCandidate("bom dia, tudo bem?") == null);
		Check("AddressCandidate(\"colete 10 pedras\") == null (sem separador)",
			CompanionNameMatch.AddressCandidate("colete 10 pedras") == null);
		Check("AddressCandidate(\"Ei, Gruncha, colete\") == null (o \"Ei\" e curto demais)",
			CompanionNameMatch.AddressCandidate("Ei, Gruncha, colete") == null);
	}

	/// <summary>
	/// Section (h): the two copies in Stubs.cs against the REAL methods in the built Companions.dll. This is what
	/// keeps a copy from drifting away from CompanionOrders.cs.
	/// </summary>
	private static void CrossCheckDoorRule()
	{
		Section("(h) as copias do Stubs.cs contra a DLL real (Companions.dll)");
		string path = FindCompanionsDll();
		if (path == null)
		{
			Skip("cross-check da DLL", "Companions.dll nao encontrada - rode dotnet build Companions.sln -c Release");
			return;
		}
		Out.WriteLine("dll: " + path);

		const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
		MethodInfo door;
		MethodInfo job;
		try
		{
			Assembly asm = Assembly.LoadFrom(path);
			Type orders = asm.GetType("Companions.CompanionOrders", true);
			door = orders.GetMethod("IsDoorRequest", flags);
			job = orders.GetMethod("JobMode", flags);
			if (door == null || job == null)
			{
				Fail("reflexao em CompanionOrders", "IsDoorRequest/JobMode nao encontrados");
				return;
			}
		}
		catch (Exception e)
		{
			Fail("Assembly.LoadFrom(" + path + ")", e.GetType().Name + ": " + e.Message);
			return;
		}

		string[] doorCorpus = new string[]
		{
			"fecha a porta", "abre a porta", "Fecha a porta", "fecha a porta?", "fecha o portão",
			"abre o teu inventário e fecha a porta", "transportar madeira", "close the door", "ferme la porte",
			"chiudi la porta", "cierra la puerta", "porta", "abre", "o que tem no teu inventário?",
			"fecha o inventário", "abre a mochila e fecha a porta", "", null
		};
		for (int i = 0; i < doorCorpus.Length; i++)
		{
			string message = doorCorpus[i];
			bool local = CompanionOrders.IsDoorRequest(message);
			bool real;
			try
			{
				real = (bool)door.Invoke(null, new object[] { message });
			}
			catch (TargetInvocationException e)
			{
				Fail("DLL IsDoorRequest(" + Show(message) + ")", Describe(e.InnerException ?? e));
				continue;
			}
			Check("IsDoorRequest(" + Show(message) + "): copia == DLL", local == real,
				"copia " + local + ", dll " + real);
		}

		string[] actions = new string[]
		{
			"wood", "stone", "ore", "forage", "smelt", "hunt", "farm", "fish", "repair", "cook",
			"attack", "equip", "drop", "follow", "stop", "none", "", null
		};
		for (int i = 0; i < actions.Length; i++)
		{
			string action = actions[i];
			int local = CompanionOrders.JobMode(action);
			int real;
			try
			{
				real = (int)job.Invoke(null, new object[] { action });
			}
			catch (TargetInvocationException e)
			{
				Fail("DLL JobMode(" + Show(action) + ")", Describe(e.InnerException ?? e));
				continue;
			}
			Check("JobMode(" + Show(action) + "): copia == DLL", local == real, "copia " + local + ", dll " + real);
		}
	}

	// ============================================================================================ the cases

	private static void RadialCase(string message, string action, string expected, string expectedReason = null)
	{
		string reason;
		string corrected;
		try
		{
			corrected = RadialIntent.Correct(action, message, out reason);
		}
		catch (Exception e)
		{
			Fail("RadialIntent.Correct(" + Show(action) + ", " + Show(message) + ")", Describe(e));
			return;
		}
		string name = "RadialIntent.Correct(" + Show(action) + ", " + Show(message) + ")";
		CheckText(name, expected, corrected);
		// the reason goes to the log: checked where the case names one, and must be null when nothing was corrected
		if (expectedReason != null || expected == null)
		{
			CheckText("  ... motivo do log", expectedReason, reason);
		}
	}

	private static void PickupCase(string message, string expected)
	{
		string actual;
		try
		{
			actual = PickupIntent.NeighborAction("nopickup", message);
		}
		catch (Exception e)
		{
			Fail("PickupIntent.NeighborAction(\"nopickup\", " + Show(message) + ")", Describe(e));
			return;
		}
		CheckText("PickupIntent.NeighborAction(\"nopickup\", " + Show(message) + ")", expected, actual);
	}

	private static void GatherCase(string action, string message, string expectedAction, int expectedAmount)
	{
		int amount;
		string actual;
		try
		{
			actual = GatherIntent.FromModelAction(action, message, out amount);
		}
		catch (Exception e)
		{
			Fail("GatherIntent.FromModelAction(" + Show(action) + ", " + Show(message) + ")", Describe(e));
			return;
		}
		CheckText("GatherIntent.FromModelAction(" + Show(action) + ", " + Show(message) + ")", expectedAction, actual);
		CheckInt("  ... quantidade de " + Show(message), expectedAmount, amount);
	}

	private static void InteractionCase(string action, string message, string expected, string expectedReason)
	{
		string reason;
		string corrected;
		try
		{
			corrected = InteractionIntent.Correct(action, message, out reason);
		}
		catch (Exception e)
		{
			Fail("InteractionIntent.Correct(" + Show(action) + ", " + Show(message) + ")", Describe(e));
			return;
		}
		string name = "InteractionIntent.Correct(" + Show(action) + ", " + Show(message) + ")";
		CheckText(name, expected, corrected);
		if (expectedReason != null || expected == null)
		{
			CheckText("  ... motivo do log", expectedReason, reason);
		}
	}

	private static void ItemCase(string action, string resource, int mode, string expected)
	{
		string actual;
		try
		{
			actual = OrderItems.Resolve(action, resource, mode);
		}
		catch (Exception e)
		{
			Fail("OrderItems.Resolve(" + Show(action) + ", " + Show(resource) + ", " + mode + ")", Describe(e));
			return;
		}
		CheckText("OrderItems.Resolve(" + Show(action) + ", " + Show(resource) + ", " + mode + ")", expected, actual);
	}

	// ============================================================================================ the plumbing

	private static void Section(string title)
	{
		Out.WriteLine();
		Out.WriteLine("=== " + title + " ===");
	}

	private static void Check(string name, bool ok, string detail = null)
	{
		if (ok)
		{
			_pass++;
			Out.WriteLine("PASS  " + name);
			return;
		}
		Fail(name, detail);
	}

	private static void CheckText(string name, string expected, string actual)
	{
		Check(name + " -> " + Show(expected), string.Equals(expected, actual, StringComparison.Ordinal),
			"obtido " + Show(actual));
	}

	/// <summary>E6: a float assertion with a small tolerance (the template offsets are metres).</summary>
	private static void CheckFloat(string name, float expected, float actual)
	{
		Check(name + " -> " + expected.ToString("0.##"), Math.Abs(expected - actual) < 0.001f, "obtido " + actual.ToString("0.###"));
	}

	private static void CheckInt(string name, int expected, int actual)
	{
		Check(name + " -> " + expected, expected == actual, "obtido " + actual);
	}

	private static void Fail(string name, string detail)
	{
		_fail++;
		string line = name + (string.IsNullOrEmpty(detail) ? "" : "  [" + detail + "]");
		Failures.Add(line);
		Out.WriteLine("FAIL  " + line);
	}

	private static void Skip(string name, string why)
	{
		_skip++;
		Skips.Add(name + " (" + why + ")");
		Out.WriteLine("SKIP  " + name + "  [" + why + "]");
	}

	/// <summary>A case whose expected value is the BUG. Not counted as PASS/FAIL: the orchestrator decides.</summary>
	private static void KnownFail(string name, bool stillBroken, string detail)
	{
		_knownFail++;
		Out.WriteLine((stillBroken ? "KNOWN-FAIL  " : "KNOWN-FAIL* ") + name + "  [" + detail + "]"
			+ (stillBroken ? "" : "  <-- o bug sumiu: a marca pode sair"));
	}

	private static string Show(string value)
	{
		return value == null ? "null" : "\"" + value + "\"";
	}

	private static string Describe(Exception e)
	{
		return e == null ? "excecao nula" : e.GetType().Name + ": " + e.Message;
	}

	/// <summary>
	/// The harness runs outside the game, so the two things the item map asks the world for are null: ObjectDB
	/// (there is no item database - Stubs.cs explains why the real type cannot be loaded here at all) and
	/// Localization (there is no language). This is the environment the real <c>Known()</c>/<c>ItemLocal()</c>/
	/// <c>MHR_List.Local()</c> are written for outside the game.
	/// </summary>
	private static void Preconditions()
	{
		Check("ObjectDB.instance == null (sem mundo: o mapa de itens é confiado)", ObjectDB.instance == null);
		try
		{
			// Localization is only read by MHR_List.Local, inside try/catch: a throw here is not fatal, but it would
			// silently disable the in-game-name lookup of the list path, so it is reported.
			if (Localization.instance != null)
			{
				Out.WriteLine("nota: Localization.instance nao e nula (harness rodando dentro do jogo?)");
			}
		}
		catch (Exception e)
		{
			Out.WriteLine("nota: Localization.instance nao carregou (" + Describe(e) + ") - o caminho da lista "
				+ "casa so pelo nome do prefab (que e o que os testes usam)");
		}
	}

	// ============================================================================================ the game assemblies

	/// <summary>
	/// The game assemblies are referenced in place (Private="false"), so the CLR needs to be pointed at them at run
	/// time - the same AssemblyResolve recipe the rest of the QA harnesses use. The folders are the ones the build
	/// used (baked in as AssemblyMetadata) plus the env overrides.
	/// </summary>
	private static void InstallAssemblyProbe()
	{
		AddProbeDir(AppDomain.CurrentDomain.BaseDirectory);
		AddProbeDir(Environment.GetEnvironmentVariable("AI_COMPANIONS_MANAGED"));
		AddProbeDir(Metadata("ManagedDir"));
		AddProbeDir(Metadata("BepInExCoreDir"));
		string valheim = Environment.GetEnvironmentVariable("VALHEIM_DIR");
		if (!string.IsNullOrEmpty(valheim))
		{
			AddProbeDir(Path.Combine(valheim, @"valheim_Data\Managed"));
			AddProbeDir(Path.Combine(valheim, @"BepInEx\core"));
		}
		string steam = @"C:\Program Files (x86)\Steam\steamapps\common\Valheim";
		AddProbeDir(Path.Combine(steam, @"valheim_Data\Managed"));
		AddProbeDir(Path.Combine(steam, @"BepInEx\core"));
		AppDomain.CurrentDomain.AssemblyResolve += ResolveFromProbeDirs;
	}

	private static void AddProbeDir(string dir)
	{
		if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
		{
			return;
		}
		if (!ProbeDirs.Contains(dir))
		{
			ProbeDirs.Add(dir);
		}
	}

	private static Assembly ResolveFromProbeDirs(object sender, ResolveEventArgs args)
	{
		string simple;
		try
		{
			simple = new AssemblyName(args.Name).Name;
		}
		catch
		{
			return null;
		}
		if (string.IsNullOrEmpty(simple))
		{
			return null;
		}
		for (int i = 0; i < ProbeDirs.Count; i++)
		{
			string candidate = Path.Combine(ProbeDirs[i], simple + ".dll");
			if (!File.Exists(candidate))
			{
				continue;
			}
			try
			{
				return Assembly.LoadFrom(candidate);
			}
			catch
			{
				// keep looking: another folder may hold a loadable copy
			}
		}
		return null;
	}

	private static string Metadata(string key)
	{
		object[] attributes = typeof(Program).Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false);
		for (int i = 0; i < attributes.Length; i++)
		{
			AssemblyMetadataAttribute attribute = attributes[i] as AssemblyMetadataAttribute;
			if (attribute != null && attribute.Key == key)
			{
				return attribute.Value;
			}
		}
		return null;
	}

	/// <summary>The built mod: the cross-check of the two copied methods reads it.</summary>
	private static string FindCompanionsDll()
	{
		string fromEnv = Environment.GetEnvironmentVariable("AI_COMPANIONS_DLL");
		if (!string.IsNullOrEmpty(fromEnv) && File.Exists(fromEnv))
		{
			return Path.GetFullPath(fromEnv);
		}
		DirectoryInfo dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
		for (int depth = 0; dir != null && depth < 8; depth++, dir = dir.Parent)
		{
			string release = Path.Combine(dir.FullName, @"src\Companions\bin\Release\net472\Companions.dll");
			if (File.Exists(release))
			{
				return release;
			}
			string debug = Path.Combine(dir.FullName, @"src\Companions\bin\Debug\net472\Companions.dll");
			if (File.Exists(debug))
			{
				return debug;
			}
		}
		return null;
	}
}
