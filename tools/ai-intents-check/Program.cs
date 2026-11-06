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
/// which is the plan index a group drives next when one of its pieces is refused with <c>no_support</c>. Section (t) is
/// the <b>body barrier</b> of the walk order (<c>BuildFailure.IsTransient</c>/<c>ShortRetry</c> and the piece's level):
/// the spot a body is standing on is retried 3 times in 2,5 s and the storeys above it wait. Section (u) is
/// <c>BuildSupportBelow.FallbackPostAllowed</c>: when the emergency post may take over from a support the plan <b>has</b>
/// but is not delivering (the Run 9 column that stayed out). Section (v) is <c>BuildStationCover.Required</c>: the
/// crafting-station rule lifted <b>only</b> for a blueprint (<c>[Building] BlueprintStations</c>), never for a template or
/// a single piece. Section (w) is <c>BuildMaterials</c>: the material need of a plan (prefab → count folded through each
/// piece's costs), the number the build panel shows before the order and <c>blueprints.inspect</c> reports as
/// <c>materials</c>.</para>
/// 
/// <para>Section (x) is <c>BuildWalkSpot</c>: <b>where the companion stands</b> to place a piece that is not at its feet
/// (Run 13, 04/11/2026 - the ridge of the <c>CommHomeBasicBP</c> walked the wrong way). The same storey first, then the
/// highest surface the game's eye rule still covers; none = the piece itself, the old target.</para>
///
/// <para>Section (z) is <c>BuildSpread</c>: <b>how far apart the crew of a site builds</b> (Run 14, 04/11/2026 - the player
/// measured five companions racing for the same corner: 88 <c>corpo na peça</c> and 32 step-aside asks). A shared plan is
/// not walked in a line: a worker takes the first piece of a small window of the same run that is far from what the others
/// are building, and the pieces it walked past keep their turn in the site's queue.</para>
///
/// <para>Section (aa) is <c>BuildSpread.Nearest</c>, the <b>opposite</b> question asked by the redesign of the build order
/// (04/11/2026): the nearest pending piece this worker can place <b>from where it stands</b> - the smallest XZ distance
/// inside a reach, with no spread distance at all - answered as the index of the caller's window (-1 = walk to today's
/// piece, byte for byte). A piece another companion already claimed is not a candidate, and a claimed piece of another
/// storey (a floor and the roof above it share the XZ) does not take the roof away.</para>
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
		Section("(t) a barreira de andar - recusa por corpo (IsTransient/ShortRetry) e o nivel da peca");
		BodyBarrier();
		Section("(u) BuildSupportBelow.FallbackPostAllowed - o poste quando o apoio do plano nao vem");
		FallbackPosts();
		Section("(v) BuildStationCover.Required - a bancada exigida so fora do blueprint");
		StationsCover();
		Section("(w) BuildMaterials - o custo do plano (necessidade de materiais)");
		Materials();
		Section("(x) BuildWalkSpot.Choose - de onde a peca e alcancada (o ponto do walk, Run 13)");
		WalkSpotCases();
		Section("(y) BuildAsideSpot.Plan - para onde o companheiro sai da frente (o StepAside do Run 14)");
		AsideSpots();
		Section("(z) BuildSpread - a dispersao dos workers (ninguem disputa a peca, Run 14)");
		SpreadCases();
		Section("(aa) BuildSpread.Nearest - a peca que eu consigo colocar daqui (o P0 do redesenho)");
		NearestCases();
		Section("(ab) BuildSpread.Chunk - o trecho de cada companheiro (a particao da obra)");
		ChunkCases();
		Section("(ac) BuildPieceKind.IsTip - a unica peca que pode ser assentada sozinha (o Forest Stone Cottage)");
		TipCases();
		Section("(ad) BuildBlueprintParser.Ordered - a viga que pende do telhado (o igloo, 05/11/2026)");
		HangingBeams();
		Section("(ae) MHR_Clearance - o corpo cabe onde ele vai? (o vao de 1 m do deck, 05/11/2026)");
		Clearance();
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
		// 04/11/2026 (the storey barrier): a body on the spot is its own class - nobody's fault, no attempt spent. The
		// probe's answer is ignored for it (it is only read for no_ground) and the comparison ignores case.
		FailureCase("blocked_by_player", true, BuildFailure.Kind.Blocked);
		FailureCase("blocked_by_player", false, BuildFailure.Kind.Blocked);
		FailureCase("BLOCKED_BY_PLAYER", true, BuildFailure.Kind.Blocked);
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
		// 04/11/2026 (the storey barrier): the yes/no of the body class, in both directions
		Check("IsBlocked(\"blocked_by_player\", true) -> true (corpo no caminho: nenhuma tentativa gasta)",
			BuildFailure.IsBlocked("blocked_by_player", true), "obtido false");
		Check("IsBlocked(\"no_support\", true) -> false (o apoio que falta nao e um corpo)",
			!BuildFailure.IsBlocked("no_support", true), "obtido true");
		// the numbers of the storey barrier, so a silent change of the window is caught here
		// 04/11/2026 (Run 17): one clock - the patience is the same 10 s as BodyBarrierSeconds (it used to be 25 s, so the
		// wait outlived the site's gate and the workers stood still)
		Check("constantes da barreira: 2 s de volta, 1 m de andar, 10 s de paciencia (o mesmo relogio da barreira)",
			Math.Abs(BuildFailure.BodyRetryDelay - 2f) < 0.001f && Math.Abs(BuildFailure.BodyRetryStep - 1f) < 0.001f
				&& Math.Abs(BuildFailure.BodyRetryMaxWait - 10f) < 0.001f,
			"obtido " + BuildFailure.BodyRetryDelay.ToString("F2") + " / " + BuildFailure.BodyRetryStep.ToString("F2")
				+ " / " + BuildFailure.BodyRetryMaxWait.ToString("F2"));

		// 04/11/2026 (the cut of the storey barrier): does the site hold a worker for a piece whose clock says dueAt?
		// True while the patience left covers it - with the lowest piece due in 20 s and one above it due in 2 s what is
		// read is the 2 s (the min(At) of the barrier) - false for a NaN clock or an exhausted patience, and a clock of
		// 0 or less is "there is no clock to read": hold, the behaviour of before this cut.
		float now = 500f;   // the site's clock, in seconds (a value where now +/- 2, 10 and 12 are exact floats)
		BodyWaitCase("BodyWaitsFor(now + 2, now, 10) -> true (2 s cabem em 10 s: o min(At) da peca de cima)", now + 2f, now, 10f, true);
		BodyWaitCase("BodyWaitsFor(now + 12, now, 10) -> false (nao cabe na paciencia: sobe)", now + 12f, now, 10f, false);
		BodyWaitCase("BodyWaitsFor(now + 10, now, 10) -> true (borda: exatamente a paciencia ainda espera)", now + 10f, now, 10f, true);
		BodyWaitCase("BodyWaitsFor(0, now, 10) -> true (sem relogio: o comportamento de antes do corte)", 0f, now, 10f, true);
		BodyWaitCase("BodyWaitsFor(NaN, now, 10) -> false (NaN nunca segura)", float.NaN, now, 10f, false);
		BodyWaitCase("BodyWaitsFor(now + 2, now, 0) -> false (paciencia esgotada)", now + 2f, now, 0f, false);
	}

	/// <summary>Section (p): one <c>BodyWaitsFor</c> case - the piece's clock (an absolute time; 0 or less = no clock),
	/// the current time, the patience left and whether the site holds a worker for that piece.</summary>
	private static void BodyWaitCase(string name, float dueAt, float now, float patience, bool expected)
	{
		bool actual = BuildFailure.BodyWaitsFor(dueAt, now, patience);
		Check(name, actual == expected, "obtido " + (actual ? "true" : "false"));
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
	/// Section (ac), 04/11/2026 - the first third-party blueprint of the sequence (the Forest Stone Cottage) failed with
	/// 24 of 64 pieces and 28 "verification failed: not_found": the plan's stone foundation sits 1,48 m under its wooden
	/// floor, the config lifted the whole plan 1,00 m and the <b>settle</b> then moved each of the 16 buried foundation
	/// pieces up to the ground <b>on its own</b> - the floors above kept the +1,00 m, nothing was holding them and the game
	/// destroyed them on the next wear tick.
	/// <para>The rule pinned here: only a buried <b>tip</b> (a pole, a beam, a pillar) may be settled alone, because its own
	/// length absorbs the move and the piece above still lands on its top (that is the reference house: 3 tips, 1,00 m
	/// lift, unchanged). A floor, a wall, a roof or a door must NEVER be moved on its own.</para>
	/// </summary>
	private static void TipCases()
	{
		// the tips: the reference house's buried poles/beams and the pillar vocabulary
		TipCase("wood_pole2", true);
		TipCase("wood_pole", true);
		TipCase("wood_beam", true);
		TipCase("wood_beam_45", true);
		TipCase("stone_pillar", true);
		// the prefab name is compared in lower case, like Classify
		TipCase("WOOD_POLE", true);
		TipCase("Wood_Beam_45", true);
		// the rigid pieces: the Forest Stone Cottage foundation (16 pieces) and everything that opens a gap above it
		TipCase("wood_floor", false);
		TipCase("stone_floor_2x2", false);
		TipCase("stone_wall_2x1", false);
		TipCase("woodwall", false);
		TipCase("wood_roof_45", false);
		TipCase("wood_wall_roof_top_45", false);
		TipCase("wood_door", false);
		TipCase("wood_stair", false);
		TipCase("piece_chest_wood", false);
		TipCase("fire_pit", false);
		// an unknown name is never moved alone (the same safe side as Classify)
		TipCase("", false);
		TipCase(null, false);
		// the measured failure, as one assertion: neither of the 16 foundation pieces may be settled on its own
		Check("a fundacao do Forest Stone Cottage (stone_floor_2x2 + stone_wall_2x1) nao e ponta",
			!BuildPieceKind.IsTip("stone_floor_2x2") && !BuildPieceKind.IsTip("stone_wall_2x1"),
			"obtido: uma das duas seria assentada sozinha");
	}

	/// <summary>Section (ac): one prefab name and whether the settle may move it on its own.</summary>
	private static void TipCase(string prefab, bool expected)
	{
		bool actual = BuildPieceKind.IsTip(prefab);
		Check("IsTip(" + Show(prefab) + ") -> " + expected, actual == expected, "obtido " + actual);
	}

	/// <summary>
	/// Section (ae), 05/11/2026 - the gap of the deck, measured with a print by the player: <c>hs_meadows_stone_cottage</c>
	/// with a SINGLE companion closed <c>peças: 64/64, 0 pulada(s)</c> and the body ended <b>0,88 m inside the terrain</b>
	/// (feet 37,90 against a ground of 38,78), squeezed under the deck's floor (a 1,00 m gap for a 1,80 m body). The game
	/// has no head room test in the movement (see <c>docs/re/deck-clearance.md</c>) and its own rescue fires only 1,00 m
	/// under the ground, so the rule is ours.
	///
	/// <para>The two halves that must never be confused: <c>MayEnter</c> refuses the step only when the <b>destination</b>
	/// does not fit <b>and</b> the place the body is in right now <b>does</b> - a body already inside the gap keeps every
	/// direction (without it the companion becomes a statue and the player has to demolish the piece, which is what
	/// happened). The geometry is pinned too: the two spheres of the capsule come from the capsule that was actually
	/// measured (<c>BuildProbe.CapsuleOf</c>), never from a constant, because the Dverger prefabs may have another body.</para>
	/// </summary>
	private static void Clearance()
	{
		// --- the rule, all four combinations: the destination decides only while the body fits where it stands
		Check("MayEnter(cabe no destino) -> true (o passo livre)",
			MHR_Clearance.MayEnter(true, true), "obtido false");
		Check("MayEnter(NAO cabe no destino, cabe aqui) -> false (nao ENTRA no vao de 1 m)",
			!MHR_Clearance.MayEnter(false, true), "obtido true");
		// the half that saves the companion: already inside a space that does not fit, every direction is allowed - the
		// way OUT must never be refused (this is the measured statue: parado, 0,02 m em 11 s)
		Check("MayEnter(NAO cabe no destino nem aqui) -> true (quem ja esta dentro sai)",
			MHR_Clearance.MayEnter(false, false), "obtido false");
		Check("MayEnter(cabe no destino, NAO cabe aqui) -> true (saindo do vao)",
			MHR_Clearance.MayEnter(true, false), "obtido false");

		// --- the spheres: the measured body (1,80 m, r 0,30) standing on the ground at 38,78
		float lower, upper, r;
		Check("esferas do corpo medido (1,80 m, r 0,30, pes 38,78) -> medidas",
			MHR_Clearance.Spheres(38.78f, 40.58f, 0.3f, 38.78f, out lower, out upper, out r),
			"obtido false");
		CheckFloat("  ... esfera de baixo = pes + r + 1 cm (38,78 + 0,30 + 0,01) = 39,09", 39.09f, lower);
		CheckFloat("  ... esfera de cima = pes + h - r = 38,78 + 1,80 - 0,30 = 40,28", 40.28f, upper);
		CheckFloat("  ... raio com 1 cm de folga (0,29)", 0.29f, r);

		// the collider's bottom is a PREFAB number: the lift between it and the feet is measured, not assumed
		Check("pes 38,78 com o collider comecando 0,20 m acima (bottom 38,98) -> medidas",
			MHR_Clearance.Spheres(38.98f, 40.78f, 0.3f, 38.78f, out lower, out upper, out r),
			"obtido false");
		CheckFloat("  ... o levantamento entra nas duas esferas (baixo 39,29)", 39.29f, lower);
		CheckFloat("  ... e a altura e a do collider, nao a do transform (cima 40,48)", 40.48f, upper);

		// a body shorter than two radii collapses to ONE sphere: CheckCapsule with the ends crossed is undefined
		Check("corpo curto (topo - fundo = 0,40 m, r 0,30) -> medidas",
			MHR_Clearance.Spheres(38.78f, 39.18f, 0.3f, 38.78f, out lower, out upper, out r),
			"obtido false");
		Check("  ... as duas esferas ficam no mesmo ponto (uma so)",
			Math.Abs(lower - upper) < 0.001f, "obtido " + lower.ToString("F2") + " / " + upper.ToString("F2"));

		// a body that cannot be measured is never a refusal: fail-open, like the rest of the probe
		Check("corpo sem medida (altura 0) -> false (nunca recusa por medida ruim)",
			!MHR_Clearance.Spheres(38.78f, 38.78f, 0.3f, 38.78f, out lower, out upper, out r), "obtido true");
		Check("corpo sem raio utilizavel (r 0,01) -> false",
			!MHR_Clearance.Spheres(38.78f, 40.58f, 0.01f, 38.78f, out lower, out upper, out r), "obtido true");

		// --- what counts as an obstacle: the load-bearing distinction is the BOTTOM, never the top
		// the measured ceiling: the deck's floor (bottom 39,67 = 0,89 m over the feet) is inside the body -> BLOCKED
		Check("o piso do deck (fundo 39,67, 0,89 m acima dos pes) -> BLOQUEIA (e o vao de 1 m medido)",
			MHR_Clearance.Blocks(39.67f, false, false, 38.78f + MHR_Clearance.StepIgnore),
			"obtido false");
		// the floor he stands on / walks onto: its collider reaches the ground, so it is not a ceiling
		Check("a telha sob os pes (fundo 38,50) -> livre",
			!MHR_Clearance.Blocks(38.5f, false, false, 38.78f + MHR_Clearance.StepIgnore),
			"obtido true");
		// a step at the foot of a stair: bottom at the ground, its body right in front of the feet -> never a ceiling
		Check("degrau com o fundo no chao (38,70) -> livre (a escada nao vira teto)",
			!MHR_Clearance.Blocks(38.70f, false, false, 38.78f + MHR_Clearance.StepIgnore),
			"obtido true");
		// a wall: bottom at the ground, top 2 m up -> libre for the ceiling test (the horizontal check is what stops him)
		Check("parede com o fundo no chao (38,72) -> livre para o teto (quem barra a parede e o teste horizontal)",
			!MHR_Clearance.Blocks(38.72f, false, false, 38.78f + MHR_Clearance.StepIgnore),
			"obtido true");
		// a low beam floating 1,20 m over the feet: inside the body -> BLOCKED
		Check("viga flutuando 1,20 m acima dos pes -> BLOQUEIA",
			MHR_Clearance.Blocks(39.98f, false, false, 38.78f + MHR_Clearance.StepIgnore),
			"obtido false");
		Check("o terreno nunca e teto (heightmap nao tem beiral)",
			!MHR_Clearance.Blocks(60f, true, false, 38.78f + MHR_Clearance.StepIgnore), "obtido true");
		Check("outro Character nao e teto (o corpo e assunto do jogo)",
			!MHR_Clearance.Blocks(41f, false, true, 38.78f + MHR_Clearance.StepIgnore), "obtido true");

		// --- the rescue and the `enterrado` count share one rule, and the measured number is the one that must fire
		Check("os 0,88 m medidos -> enterrado (acima da margem de 0,40 m)",
			MHR_Clearance.IsBuried(0.88f), "obtido false");
		Check("0,40 m exato -> nao e enterrado (a margem e o teto)",
			!MHR_Clearance.IsBuried(MHR_Clearance.BuriedDepth), "obtido true");
		Check("0,39 m (terreno irregular, degrau) -> nao e enterrado",
			!MHR_Clearance.IsBuried(0.39f), "obtido true");
		Check("acima do terreno (profundidade negativa) -> nao e enterrado",
			!MHR_Clearance.IsBuried(-1f), "obtido true");

		// the numbers the rule walks with: a silent change of them is caught here
		Check("constantes: 1 cm de folga, 0,25 m da faixa do degrau, 0,40 m de enterrado, 1,20 m de sonda",
			Math.Abs(MHR_Clearance.Slack - 0.01f) < 0.0001f && Math.Abs(MHR_Clearance.StepIgnore - 0.25f) < 0.0001f
				&& Math.Abs(MHR_Clearance.BuriedDepth - 0.4f) < 0.0001f
				&& Math.Abs(MHR_Clearance.LookAhead - 1.2f) < 0.0001f,
			"obtido " + MHR_Clearance.Slack.ToString("F3") + " / " + MHR_Clearance.StepIgnore.ToString("F2") + " / "
				+ MHR_Clearance.BuriedDepth.ToString("F2") + " / " + MHR_Clearance.LookAhead.ToString("F2"));
		// the look-ahead has to land the refusal BEFORE the body touches: 1,2 m minus the 0,30 m radius of the measured body
		Check("a sonda deixa ~0,9 m de margem antes do toque (1,20 - 0,30)",
			MHR_Clearance.LookAhead - 0.3f > 0.8f, "obtido " + (MHR_Clearance.LookAhead - 0.3f).ToString("F2"));
		// no zero in the deviations: that is the direction that was just refused
		bool noZero = true;
		for (int i = 0; i < MHR_Clearance.Deviations.Length; i++)
		{
			if (Math.Abs(MHR_Clearance.Deviations[i]) < 0.001f) noZero = false;
		}
		Check("desvios laterais sem zero (" + MHR_Clearance.Deviations.Length + " direcoes)",
			noZero && MHR_Clearance.Deviations.Length == 6, "obtido " + MHR_Clearance.Deviations.Length);
	}

	/// <summary>
	/// Section (ad), 05/11/2026 - o igloo medido: 44 s -> 104 s com **86,4 s de `dependência`** e 40 desistências do poste
	/// de emergência, todas em `wood_beam` a 3,90/4,90/7,90 m do piso. Num domo a viga se apoia nas águas CURVAS do
	/// telhado (classe 3), que vinham DEPOIS: a viga era tentada antes, recusada por `no_support` e esperava 8/12/20 s.
	/// A regra que o `Ordered` passou a aplicar: uma viga (classe 2) que <b>não tem nada embaixo no plano</b> (0,4 a 2,5 m
	/// abaixo, 2,5 m em XZ) e <b>tem telhado ao lado</b> (classe 3, |Δy| ≤ 2,5 m) é construída <b>junto do telhado</b>.
	/// Uma viga com parede/viga embaixo continua classe 2 - é o caso da casa normal.
	/// </summary>
	private static void HangingBeams()
	{
		// a viga 3 m acima do piso, com telhado ao lado e uma PAREDE no mesmo nível: ela vai DEPOIS da parede (classe 3),
		// junto do telhado. Sem a regra ela viria antes da parede (classe 2).
		CheckText("viga que pende do telhado vai junto do telhado (depois da parede)",
			"wood_floor wood_wall wood_beam wood_roof_45",
			TracePrefabs(BuildBlueprintParser.Ordered(Plan(
				Piece("wood_floor", 0f, 0f, 0f),
				Piece("wood_beam", 0f, 3f, 0f),
				Piece("wood_roof_45", 2f, 3f, 0f),
				Piece("wood_wall", 0f, 3f, 2f)))));

		// a mesma viga COM uma parede 1 m embaixo: aí ela é viga de verdade e NÃO entra no conjunto `hangs`. A ordem sai
		// igual à de cima, mas por outro motivo - a parede está num NÍVEL menor (y=2), e a chave de nível vem antes da
		// classe. O que este caso fixa é que a viga com apoio embaixo continua classe 2 (o conjunto não a pega).
		CheckText("viga com parede embaixo continua classe 2 (não é 'pendurada')",
			"wood_floor wood_wall wood_beam wood_roof_45",
			TracePrefabs(BuildBlueprintParser.Ordered(Plan(
				Piece("wood_floor", 0f, 0f, 0f),
				Piece("wood_beam", 0f, 3f, 0f),
				Piece("wood_roof_45", 2f, 3f, 0f),
				Piece("wood_wall", 0f, 2f, 0f)))));

		// sem telhado ao lado não há o que segurar: continua classe 2
		CheckText("viga sem telhado ao lado não muda de classe",
			"wood_floor wood_beam wood_wall",
			TracePrefabs(BuildBlueprintParser.Ordered(Plan(
				Piece("wood_floor", 0f, 0f, 0f),
				Piece("wood_beam", 0f, 3f, 0f),
				Piece("wood_wall", 0f, 3f, 2f)))));
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
		// the two gaps the house of 11/11 actually had, with MaxOverlap 0,45 (0,35 left both without a post): the step between
		// two floors is 0,60 m and the 1 m beam crosses 0,40 m of the piece (support is collider overlap, not exact length);
		// the 1,6 m one is reached by the 2 m beam. 1,30 m is the hole between the 1 m and the 2 m windows.
		PostCase(posts, 0.6f, 0, "o vão medido de 0,60 m (o degrau entre dois pisos) -> viga de 1 m (com 0,35 nao cabia)");
		PostCase(posts, 1.3f, -1, "vão de 1,3 m cai no buraco: 1 m fica curto (0,1 m alem da tolerancia) e 2 m passa 0,7 m");
		PostCase(posts, 1.6f, 1, "o outro vão medido, 1,6 m (entre as janelas de 1 m e de 2 m) -> viga de 2 m");
		PostCase(new[] { 0f, 2f }, 2f, 1, "prefab que o jogo nao tem (comprimento 0) e pulado");
		PostCase(new[] { 2.83f, 1f, 2f }, 1f, 1, "a escolha e pelo COMPRIMENTO, nao pela ordem da lista");
		PostCase(null, 1f, -1, "sem lista, sem poste");
		Check("ShortTolerance == 0,20 m", Math.Abs(BuildSupportBelow.ShortTolerance - 0.2f) < 0.001f,
			"obtido " + BuildSupportBelow.ShortTolerance.ToString("F2"));
		Check("MaxOverlap == 0,45 m (0,35 deixava o vão medido de 0,60 m sem poste)", Math.Abs(BuildSupportBelow.MaxOverlap - 0.45f) < 0.001f,
			"obtido " + BuildSupportBelow.MaxOverlap.ToString("F2"));
		// the WALK of the post ray (04/11/2026: a roof eave 0,16 m under the floor of 40,8 stopped the ray 318 times, while
		// the deck floor the post wants was 1,02 m down)
		SurfaceCase(new[] { 0.16f, 1.02f }, new[] { true, true }, 2, 0.4f, 1, "o caso medido: a beira do telhado a 0,16 e o deck a 1,02 -> o deck");
		SurfaceCase(new[] { 1.02f }, new[] { true }, 1, 0.4f, 0, "só o deck: ele mesmo");
		SurfaceCase(new[] { 0.16f, 0.35f }, new[] { true, true }, 2, 0.4f, -1, "duas superfícies coladas: nenhuma serve (terreno)");
		SurfaceCase(new[] { 0.4f }, new[] { true }, 1, 0.4f, 0, "borda: 0,40 m já serve de base");
		SurfaceCase(new[] { 0.39f }, new[] { true }, 1, 0.4f, -1, "0,39 m ainda é colado");
		SurfaceCase(new[] { 0.5f, 0.16f, 1.02f }, new[] { true, true, true }, 3, 0.4f, 0, "o primeiro utilizável manda (a ordem do raio)");
		SurfaceCase(new[] { 0.16f, 1.02f, 9f }, new[] { true, true, true }, 2, 0.4f, 1, "só as `count` primeiras entram");
		SurfaceCase(new float[] { }, new bool[] { }, 0, 0.4f, -1, "nenhum hit -> terreno");
		SurfaceCase(null, null, 0, 0.4f, -1, "sem lista -> terreno");
		// the surface has to HOLD, not just exist (04/11/2026: 95 refusals of the post itself, `- no_support`, because it was
		// planted deep enough but over the top of a chest - furniture carries no support, so the post had nothing under it)
		SurfaceCase(new[] { 0.5f, 1.2f }, new[] { false, true }, 2, 0.4f, 1, "a de 0,5 m não segura (baú) e a de 1,2 m segura -> a de 1,2 m");
		SurfaceCase(new[] { 0.5f, 1.2f }, new[] { false, false }, 2, 0.4f, -1, "nenhuma das duas segura -> terreno");
		// the SUPPORT OF THE PLACED PIECE (FootprintSupported): a roof piece has m_supports = false in Valheim and is held by
		// the EDGE of the row below - the 13 pieces that stayed out for five rounds and the 318 "vão de 0,16 m"
		HoldCase(3, false, true, false, true, "telhado sobre telhado: a fileira de baixo apoia pela borda (o caso medido)");
		HoldCase(3, false, true, true, true, "telhado no topo de parede/viga");
		HoldCase(3, true, false, false, true, "telhado sobre o terreno");
		HoldCase(0, false, true, true, true, "piso sobre piso/viga que carrega");
		HoldCase(0, false, true, false, false, "piso NAO e apoiado por telhado (m_supports = false)");
		HoldCase(2, false, true, false, false, "parede NAO e apoiada por telhado");
		HoldCase(1, false, true, false, false, "escada NAO e apoiada por mobilia (m_supports = false)");
		HoldCase(0, false, false, false, false, "pedra/arvore nao tem WearNTear: nao apoia");
		HoldCase(3, false, false, false, false, "telhado tambem nao e apoiado por pedra");
		HoldCase(4, false, true, true, true, "porta apoiada por parede");
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

	/// <summary>Section (r): one walk of the post ray and the surface it should use.</summary>
	private static void SurfaceCase(float[] depths, bool[] holds, int count, float minGap, int expected, string why)
	{
		int actual = BuildSupportBelow.UsefulSurface(depths, holds, count, minGap);
		Check("UsefulSurface(" + count + " hit(s), minGap " + minGap.ToString("F2") + ") -> " + expected + " (" + why + ")",
			actual == expected, "obtido " + actual);
	}

	/// <summary>Section (r): one collider of the footprint and whether it holds the piece being placed.</summary>
	private static void HoldCase(int pieceClass, bool terrain, bool wear, bool supports, bool expected, string why)
	{
		bool actual = BuildSupportBelow.HoldsPlacedPiece(pieceClass, terrain, wear, supports);
		Check("HoldsPlacedPiece(peca " + pieceClass + ", terreno=" + (terrain ? "sim" : "nao") + ", wear="
			+ (wear ? "sim" : "nao") + ", m_supports=" + (supports ? "sim" : "nao") + ") -> "
			+ (expected ? "true" : "false") + " (" + why + ")", actual == expected, "obtido " + (actual ? "true" : "false"));
	}

	/// <summary>
	/// P1 (04/11/2026) and 04/11/2026 (Run 10): the <b>body window</b> of the placement check
	/// (<c>BuildProbe.BlockedByCharacter</c>). The measured bug of 04/11: with the 1 m deck, a floor 1 m above the feet of
	/// two companions was 1,0 m from the piece's point in 3D, so the old point-vs-transform test (0,6 m) let it through and
	/// the floor went <b>over their heads</b> (squeezed to y 37,9, 1 m under the ground). Since the Run 10 the window is the
	/// game's own shape: the character's <b>capsule</b> against the piece's <b>rotated box</b> - two windows, the vertical
	/// one (the piece's AABB span vs the capsule's ends) and the flat one (the distance from the capsule's axis to the box,
	/// in the <b>piece's</b> frame).
	/// </summary>
	private static void BodyWindows()
	{
		// the measured floor (wood_floor: 2,27 x 0,26 x 2,29 -> half 1,135 / 0,13 / 1,145) at the deck level 39,8 over a
		// companion standing on the ground at 38,78 (capsule 38,78-40,58, r 0,3): the floor crosses the body -> blocked
		BodyCase("o caso medido: piso do deck (39,8) sobre o companheiro (pes 38,78)", true,
			0f, 0f, 1.135f, 1.145f, 39.67f, 39.93f, 38.78f, 40.58f, 0.3f);
		// the same floor 2,5 m to the side (past the box and the capsule) -> free: only the box's real width counts now
		BodyCase("o mesmo piso a 2,5 m do companheiro", false,
			2.5f, 0f, 1.135f, 1.145f, 39.67f, 39.93f, 38.78f, 40.58f, 0.3f);
		// ... and at 1,5 m: the old DISC (radius 1,59 = flat + 0,45) refused it, the box does not - dx = 1,5 - 1,135 = 0,365
		// > 0,3 -> free. It is a FALSE POSITIVE the disc created (a companion beside the tile held it)
		BodyCase("o mesmo piso a 1,5 m (o disco de 1,59 m recusava)", false,
			1.5f, 0f, 1.135f, 1.145f, 39.67f, 39.93f, 38.78f, 40.58f, 0.3f);
		// a floor above the head: the body ends at 40,58 and the piece starts at 41,67 -> free
		BodyCase("piso 2 m acima da cabeca", false,
			0f, 0f, 1.135f, 1.145f, 41.67f, 41.93f, 38.78f, 40.58f, 0.3f);
		// a floor at the feet (the piece under the companion): 38,65-38,91 crosses the capsule -> blocked
		BodyCase("piso no nivel dos pes", true,
			0f, 0f, 1.135f, 1.145f, 38.65f, 38.91f, 38.78f, 40.58f, 0.3f);
		// a wall (2,25 x 2,16 x 0,53 -> half 1,125 / 1,08 / 0,265): its base at 38,72 crosses the body -> blocked
		BodyCase("parede de 2 m com a base na altura do corpo", true,
			0f, 0f, 1.125f, 0.265f, 38.72f, 40.88f, 38.78f, 40.58f, 0.3f);
		// the same wall one metre higher: base 40,92, body ends at 40,58 -> free (the companion walks under it)
		BodyCase("parede inteira acima da cabeca", false,
			0f, 0f, 1.125f, 0.265f, 40.92f, 43.08f, 38.78f, 40.58f, 0.3f);
		// a companion standing ON the deck (feet 39,93) and the deck tile at 39,8: its top IS the capsule's bottom, so it does
		// not block - neither at the neighbouring tile (2 m) nor on the seam (1,4 m). The old disc refused the seam: a FALSE
		// POSITIVE (the tile under a companion was held by the companion itself)
		BodyCase("companheiro de pe no deck: a telha vizinha (2 m)", false,
			2f, 0f, 1.135f, 1.145f, 39.67f, 39.93f, 39.93f, 41.73f, 0.3f);
		BodyCase("companheiro na emenda das telhas (1,4 m) - o disco recusava, o topo da peca e o pe do corpo", false,
			1.4f, 0f, 1.135f, 1.145f, 39.67f, 39.93f, 39.93f, 41.73f, 0.3f);
		// Run 10 (measured): the wood_beam at 41,8 (half height 0,1 -> bottom 41,7) and the Companion 6 1,08 m away in XZ with
		// the capsule 39,9-41,7: the beam's bottom is exactly the capsule's top -> FREE (the game accepts it, the disc did not)
		BodyCase("Run 10: feixe de 2 m (41,7) sobre a capsula 39,9-41,7 -> livre", false,
			1f, -0.4f, 1f, 0.2f, 41.7f, 41.9f, 39.9f, 41.7f, 0.3f);
		// the same geometry with the capsule 20 cm taller: the beam's bottom is INSIDE the body -> blocked (the capsule decides)
		BodyCase("o mesmo feixe com a capsula ate 41,9 -> bloqueia", true,
			1f, -0.4f, 1f, 0.2f, 41.7f, 41.9f, 39.9f, 41.9f, 0.3f);
		// the pivot of a roof: its box centre sits 1,5 m ABOVE the pose (bounds center - prefab origin), so with the pose 0,5 m
		// over the feet the real bottom is point + 1,3 = 40,58 (the head) and not point - 0,2 = 39,08 -> free
		BodyCase("pivo do telhado (centro +1,5 m): o fundo real e point+1,3, nao point-0,2", false,
			0f, 0f, 1f, 1f, 40.58f, 40.98f, 38.78f, 40.58f, 0.3f);
		// a 45° beam: the AABB of the turned box is 0,78 m tall (HalfY) instead of 0,1 m - at 40,9 its bottom is 40,12, inside
		// the capsule, and it blocks; without the tilt (0,1 m) the same centre would be above the head
		float tilt = BuildBody.HalfY(1f, 0.1f, 0.1f, 0.70710678f, 0.70710678f, 0f);
		Check("viga de 45 graus: HalfY(1; 0,1; 0,1) == 0,78 m", Math.Abs(tilt - 0.778f) < 0.01f,
			"obtido " + tilt.ToString("F3"));
		BodyCase("viga de 45 graus (centro 40,9 - 0,78 = 40,12) sobre a capsula -> bloqueia", true,
			0f, 0f, 1f, 0.1f, 40.9f - tilt, 40.9f + tilt, 38.78f, 40.58f, 0.3f);
		BodyCase("a mesma viga sem a inclinacao (meia altura 0,1) -> livre", false,
			0f, 0f, 1f, 0.1f, 40.8f, 41f, 38.78f, 40.58f, 0.3f);
		// a companion BESIDE a wall (0,5 m from its middle plane, the wall 0,2 m thick): 0,5 - 0,1 = 0,4 > 0,3 -> free, and
		// 0,35 - 0,1 = 0,25 < 0,3 -> blocked. The pair pins the capsule's radius (and says a body in front of a wall is not
		// "inside" it, which the old disc could not tell)
		BodyCase("companheiro ao lado da parede (0,5 m do plano medio)", false,
			0f, 0.5f, 1.125f, 0.1f, 38.72f, 40.88f, 38.78f, 40.58f, 0.3f);
		BodyCase("companheiro encostado na parede (0,35 m do plano medio)", true,
			0f, 0.35f, 1.125f, 0.1f, 38.72f, 40.88f, 38.78f, 40.58f, 0.3f);
		// a companion DEBAIXO do piso (XZ dentro da peca): the floor crosses the body -> blocked, whatever the pose's y is
		BodyCase("companheiro debaixo do piso (XZ dentro)", true,
			0f, 0f, 1.135f, 1.145f, 39.87f, 40.13f, 38.78f, 40.58f, 0.3f);
	}

	/// <summary>04/11/2026 (Run 10): one case of the body window - the expected answer with the numbers
	/// <c>BuildBody.Blocks</c> takes (local XZ of the capsule's axis, the piece's half extents, its vertical span and the
	/// capsule's ends and radius).</summary>
	private static void BodyCase(string name, bool expected, float localX, float localZ, float halfX, float halfZ,
		float pieceBottom, float pieceTop, float capsuleBottom, float capsuleTop, float capsuleRadius)
	{
		bool actual = BuildBody.Blocks(localX, localZ, halfX, halfZ, pieceBottom, pieceTop, capsuleBottom, capsuleTop,
			capsuleRadius);
		Check(name + " -> " + (expected ? "bloqueia" : "livre"), actual == expected,
			"obtido " + (actual ? "bloqueia" : "livre"));
	}

	/// <summary>
	/// 04/11/2026 (the <b>body barrier</b>, the player's rule after the Run 9: "they may skip, but come back and try again
	/// before going up"): the refusal the barrier exists for is <b>only</b> <c>blocked_by_player</c> - the player walking
	/// over the spot. <c>IsTransient</c> is the yes/no (the dependency reasons of the support gate and every real failure
	/// of the piece/spot stay out of it), <c>ShortRetry</c> is the 3-short-retry cap that lets the SAME pose be tried again
	/// in 2,5 s (site-wide: a piece must not get 3 per companion), and the four constants are the barrier itself. The
	/// last three cases are the piece's <b>level</b> (<c>BuildBlueprintParser.Line.Level</c>, the very key <c>Ordered</c>
	/// sorts by): the roof belongs to the slab it covers, a stair to its own height, and filling it must not change the
	/// order.
	/// </summary>
	private static void BodyBarrier()
	{
		// IsTransient: the body and only the body (13 checks over the 14 inputs - "" and null are the same rule: a failure
		// nobody named is not a body)
		TransientCase("blocked_by_player", true);
		TransientCase("BLOCKED_BY_PLAYER", true);
		TransientCase("no_support", false);
		TransientCase("no support", false);
		TransientCase("needs_station", false);
		TransientCase("no_ground", false);
		TransientCase("clipping", false);
		TransientCase("too_close_piece", false);
		TransientCase("no progress", false);
		TransientCase("can't reach it", false);
		TransientCase("took too long", false);
		TransientCase("verification: not_found", false);
		Check("IsTransient(\"\") e IsTransient(null) -> false (falha sem nome nao e um corpo)",
			!BuildFailure.IsTransient("") && !BuildFailure.IsTransient(null),
			"obtido " + BuildFailure.IsTransient("") + " / " + BuildFailure.IsTransient(null));

		// ShortRetry: 0/1/2 yes (the three short retries), 3 and beyond no; case does not matter; another reason never
		ShortRetryCase("blocked_by_player", 0, true);
		ShortRetryCase("blocked_by_player", 1, true);
		ShortRetryCase("blocked_by_player", 2, true);
		ShortRetryCase("blocked_by_player", 3, false);
		ShortRetryCase("blocked_by_player", 4, false);
		ShortRetryCase("BLOCKED_BY_PLAYER", 0, true);
		ShortRetryCase("no_support", 0, false);
		ShortRetryCase("clipping", 0, false);
		ShortRetryCase("no progress", 0, false);
		ShortRetryCase("needs_station", 0, false);

		// the numbers of the barrier, so a silent change of the window is caught here
		// 04/11/2026 (Run 17): one clock - 10 s is the 3 short retries (7,5 s) rounded up, and ServeBarrier never outlives
		// the site's gate (it used to be 20 s of gate against 25 s of wait)
		Check("constantes: 3 curtas, 2,5 s de espera, 1,5 m de mesmo andar, 10 s de barreira",
			BuildFailure.MaxShortRetries == 3 && Math.Abs(BuildFailure.ShortRetryDelay - 2.5f) < 0.001f
				&& Math.Abs(BuildFailure.BodySameStorey - 1.5f) < 0.001f
				&& Math.Abs(BuildFailure.BodyBarrierSeconds - 10f) < 0.001f,
			"obtido " + BuildFailure.MaxShortRetries + " / " + BuildFailure.ShortRetryDelay.ToString("F2") + " / "
				+ BuildFailure.BodySameStorey.ToString("F2") + " / " + BuildFailure.BodyBarrierSeconds.ToString("F2"));

		// 04/11/2026 (Run 17): the wait of a piece refused by a body grows with the refusals and stops at 6 s - the piece
		// stays in the site's queue, but a PARKED player can no longer make a worker walk to it every other piece (36
		// bounces in the Run 16 log). 0/1 = the walking player, 2 = 4 s, 3 and beyond = 6 s.
		BlockedDelayCase(0, 2f);
		BlockedDelayCase(1, 2f);
		BlockedDelayCase(2, 4f);
		BlockedDelayCase(3, 6f);
		BlockedDelayCase(9, 6f);

		// the LEVEL of each piece: the roof is of the storey of the slab it covers (measured key, 3 m above)
		System.Collections.Generic.List<BuildBlueprintParser.Line> covered = Plan(
			Piece("wood_roof_45", 0f, 0f, 0f),
			Piece("wood_floor", 0f, 3f, 0f));
		System.Collections.Generic.List<BuildBlueprintParser.Line> coveredOrder = BuildBlueprintParser.Ordered(covered);
		int roof = IndexOfPrefab(coveredOrder, "wood_roof_45");
		Check("nivel: o telhado herda o andar do piso que cobre -> 3 (o caso medido da laje 3 m acima)",
			roof >= 0 && Math.Abs(coveredOrder[roof].Level - 3f) < 0.001f,
			"obtido " + (roof >= 0 ? coveredOrder[roof].Level.ToString("0.##") : "sem telhado"));
		// a stair has its own level (a floor on the same height is not "above" it: nothing is inherited)
		System.Collections.Generic.List<BuildBlueprintParser.Line> stair = BuildBlueprintParser.Ordered(Plan(
			Piece("wood_stair", 0f, 2f, 0f),
			Piece("wood_floor", 0f, 2f, 0f)));
		int ladder = IndexOfPrefab(stair, "wood_stair");
		Check("nivel: a escada tem o proprio nivel -> 2 (nao herda o piso do mesmo nivel)",
			ladder >= 0 && Math.Abs(stair[ladder].Level - 2f) < 0.001f,
			"obtido " + (ladder >= 0 ? stair[ladder].Level.ToString("0.##") : "sem escada"));
		// filling the level does not change the order: floor/wall of the ground floor, then the upper floor, then the roof
		System.Collections.Generic.List<BuildBlueprintParser.Line> mixed = Plan(
			Piece("wood_roof_45", 0f, 0f, 0f),
			Piece("wood_floor", 0f, 3f, 0f),
			Piece("woodwall", 0f, 0f, 0f),
			Piece("wood_floor", 0f, 0f, 0f));
		CheckText("nivel: preencher o nivel nao muda a ordem", "wood_floor woodwall wood_floor wood_roof_45",
			TracePrefabs(BuildBlueprintParser.Ordered(mixed)));
	}

	/// <summary>Section (t): one refusal and whether it is only a body on the spot.</summary>
	private static void TransientCase(string why, bool expected)
	{
		bool actual = BuildFailure.IsTransient(why);
		Check("IsTransient(" + Show(why) + ") -> " + (expected ? "true" : "false"), actual == expected,
			"obtido " + (actual ? "true" : "false"));
	}

	/// <summary>Section (t): one refusal with a number of short retries already spent.</summary>
	private static void ShortRetryCase(string why, int tries, bool expected)
	{
		bool actual = BuildFailure.ShortRetry(why, tries);
		Check("ShortRetry(" + Show(why) + ", " + tries + ") -> " + (expected ? "true" : "false"), actual == expected,
			"obtido " + (actual ? "true" : "false"));
	}

	/// <summary>Section (t): one number of body refusals already counted and the wait the site's queue gives the piece.</summary>
	private static void BlockedDelayCase(int blocks, float expected)
	{
		float actual = BuildFailure.BlockedDelay(blocks);
		Check("BlockedDelay(" + blocks + ") -> " + expected.ToString("F0") + " s", Math.Abs(actual - expected) < 0.001f,
			"obtido " + actual.ToString("F2"));
	}

	/// <summary>
	/// 04/11/2026 (the Run 9: the <c>wood_floor</c> tile at 39,8 was refused by a body, went to the site's queue and the guard
	/// "never a post where the plan has a support" then froze the three floors above it): the <b>emergency post</b> may take
	/// over from a support the plan <b>has</b> only when that support is not coming - it was <b>given up</b>, or nobody
	/// <b>claims</b> it and the refused piece already waited <c>SupportGrace</c> deferrals (the third look, ~20 s = 8 s +
	/// 12 s of the dependency waits). A support another companion holds is on its way: the piece waits for it.
	/// </summary>
	private static void FallbackPosts()
	{
		FallbackCase(true, true, 0, true, "apoio desistido: o poste assume, mesmo com outro companheiro em cima");
		FallbackCase(false, true, 5, false, "apoio reivindicado: esta chegando, a peca espera (mesmo com 5 esperas)");
		FallbackCase(false, false, 0, false, "sem espera nenhuma: o plano ainda tem a chance de entregar o apoio");
		FallbackCase(false, false, 1, false, "1 espera (8 s): ainda nao (o caso que congelou a coluna do Run 9)");
		FallbackCase(false, false, 2, true, "2 esperas (~20 s): o apoio do plano nao veio - o poste assume");
		Check("SupportGrace == 2 (o terceiro olhar, ~20 s = 8 + 12 s)", BuildSupportBelow.SupportGrace == 2,
			"obtido " + BuildSupportBelow.SupportGrace);
	}

	/// <summary>Section (u): one question about the plan's own support and the post that may take over from it.</summary>
	private static void FallbackCase(bool givenUp, bool claimed, int deferrals, bool expected, string why)
	{
		bool actual = BuildSupportBelow.FallbackPostAllowed(givenUp, claimed, deferrals);
		Check("FallbackPostAllowed(desistido=" + (givenUp ? "sim" : "nao") + ", reivindicado=" + (claimed ? "sim" : "nao")
			+ ", esperas=" + deferrals + ") -> " + (expected ? "true" : "false") + " (" + why + ")", actual == expected,
			"obtido " + (actual ? "true" : "false"));
	}

	/// <summary>
	/// Section (v): lote 1 (player's request, 04/11/2026) - the workbench/stonecutter requirement lifted <b>only</b> for a
	/// blueprint. The table is <c>BuildStationCover.Required(isBlueprint, config)</c>: a template and a single piece are
	/// <b>never</b> waived (the switch does not reach them), and a blueprint is waived only with
	/// <c>[Building] BlueprintStations</c> on.
	/// </summary>
	private static void StationsCover()
	{
		StationCase(false, true, true, "template com a chave ligada: continua exigindo (a chave nao alcanca o template)");
		StationCase(false, false, true, "template com a chave desligada: exige (regra vanilla)");
		StationCase(true, true, false, "blueprint com a chave ligada: dispensada (sem bancada planejada)");
		StationCase(true, false, true, "blueprint com a chave desligada: exige (regra vanilla)");
		// the config entry can be missing (a failed read): the fallback is the switch's default, and it is the same number
		// MHR_Build.StationRequired uses - a blueprint with no config is waived
		Check("sem config (padrao " + (BuildStationCover.Default ? "ligada" : "desligada") + "): blueprint nao exige",
			BuildStationCover.Required(true, BuildStationCover.Default) == false,
			"obtido " + (BuildStationCover.Required(true, BuildStationCover.Default) ? "true" : "false")
			+ " (Default=" + (BuildStationCover.Default ? "true" : "false") + ")");
	}

	/// <summary>Section (v): one kind of order with the switch in one position.</summary>
	private static void StationCase(bool isBlueprint, bool config, bool expected, string why)
	{
		bool actual = BuildStationCover.Required(isBlueprint, config);
		Check("Required(blueprint=" + (isBlueprint ? "sim" : "nao") + ", config=" + (config ? "ligada" : "desligada")
			+ ") -> " + (expected ? "true" : "false") + " (" + why + ")", actual == expected,
			"obtido " + (actual ? "true" : "false"));
	}

	// ============================================================================================ (w) o custo do plano

	/// <summary>
	/// Section (w): <c>BuildMaterials</c>, the material need of a plan - the pure half of what <c>BuildStock</c> and
	/// <c>MHR_Build.TemplateMissing</c> already do per piece, shared with the build panel (the house's cost before the
	/// order) and with <c>blueprints.inspect</c> (<c>materials</c>). The rules asserted here are the ones the order pays
	/// with: <c>requirement.GetAmount(1)</c> keyed by the shared item name, and an entry with no item or a
	/// non-positive amount ignored.
	/// </summary>
	private static void Materials()
	{
		// 1) the same piece counted twice multiplies its own cost
		Dictionary<string, int> total = new Dictionary<string, int>();
		BuildMaterials.Add(total, Costs(One("$item_wood", 4)), 2);
		CheckInt("2 pecas iguais multiplicam (4 x 2)", 8, Amount(total, "$item_wood"));

		// 2) a prefab that lists the same resource twice adds up inside itself
		total = new Dictionary<string, int>();
		BuildMaterials.Add(total, Costs(One("$item_wood", 2), One("$item_wood", 3)), 1);
		CheckInt("recurso repetido no mesmo prefab soma (2 + 3)", 5, Amount(total, "$item_wood"));

		// 3) a prefab with no recipe at all: no cost, and it is counted as "no recipe"
		int noRecipe;
		Dictionary<string, int> plan = new Dictionary<string, int> { { "wood_floor", 3 }, { "mystery", 2 } };
		Dictionary<string, int> need = BuildMaterials.Total(plan, RecipeOf, out noRecipe);
		Check("peca sem receita custa 0 e conta em noRecipe (noRecipe=1, sem a chave)",
			noRecipe == 1 && !need.ContainsKey("mystery"), "noRecipe=" + noRecipe + ", chaves=" + need.Count);

		// 4) an empty plan needs nothing (and has no "no recipe" to report)
		int emptyNoRecipe;
		Dictionary<string, int> empty = BuildMaterials.Total(new Dictionary<string, int>(), RecipeOf, out emptyNoRecipe);
		Check("plano vazio: nada necessario e noRecipe=0", empty.Count == 0 && emptyNoRecipe == 0,
			"itens=" + empty.Count + ", noRecipe=" + emptyNoRecipe);

		// 5) a piece counted zero times adds nothing
		total = new Dictionary<string, int>();
		int zeroIgnored = BuildMaterials.Add(total, Costs(One("$item_wood", 4)), 0);
		Check("count=0 nao soma nada e nao ignora entrada", total.Count == 0 && zeroIgnored == 0,
			"itens=" + total.Count + ", ignoradas=" + zeroIgnored);

		// 6) an entry with no item name or with a non-positive amount is ignored and counted
		total = new Dictionary<string, int>();
		int ignored = BuildMaterials.Add(total,
			Costs(One(null, 5), One("", 5), One("$item_wood", 0), One("$item_wood", 7)), 1);
		Check("item nulo/vazio e quantidade <= 0 ignorados e contados (3)",
			ignored == 3 && total.Count == 1 && Amount(total, "$item_wood") == 7,
			"ignoradas=" + ignored + ", itens=" + total.Count + ", wood=" + Amount(total, "$item_wood"));

		// 7) two different pieces that share a resource: the need is the sum of both
		Dictionary<string, int> mixed = new Dictionary<string, int> { { "wood_floor", 1 }, { "wood_wall", 2 } };
		int mixedNoRecipe;
		Dictionary<string, int> mixedNeed = BuildMaterials.Total(mixed, RecipeOf, out mixedNoRecipe);
		CheckInt("2 pecas diferentes com o mesmo recurso somam (4 + 2x2)", 8, Amount(mixedNeed, "$item_wood"));

		// 8) a big plan stays positive (no overflow to a negative number)
		total = new Dictionary<string, int>();
		BuildMaterials.Add(total, Costs(One("$item_wood", 1000)), 1000);
		CheckInt("valor alto nao vira negativo (1000 x 1000)", 1000000, Amount(total, "$item_wood"));

		// 9) Total has no state between calls: a fresh dictionary, the same answer
		int firstNoRecipe;
		int secondNoRecipe;
		Dictionary<string, int> first = BuildMaterials.Total(plan, RecipeOf, out firstNoRecipe);
		Dictionary<string, int> second = BuildMaterials.Total(plan, RecipeOf, out secondNoRecipe);
		Check("Total 2x em dicionario novo da o mesmo resultado",
			Same(first, second) && firstNoRecipe == secondNoRecipe,
			"1a=" + Amount(first, "$item_wood") + "/" + firstNoRecipe + ", 2a=" + Amount(second, "$item_wood") + "/" + secondNoRecipe);
	}

	/// <summary>Section (w): one cost line (the struct's own name is taken by the type, so the helper is "One").</summary>
	private static Cost One(string item, int amount)
	{
		return new Cost { Item = item, Amount = amount };
	}

	/// <summary>Section (w): the cost list of one piece.</summary>
	private static IReadOnlyList<Cost> Costs(params Cost[] costs)
	{
		return costs;
	}

	/// <summary>
	/// Section (w): the resolver <c>BuildMaterials.Total</c> receives - prefab -&gt; its costs. <c>wood_floor</c> is 4
	/// wood and <c>wood_wall</c> is 2 wood (two different pieces sharing a resource); anything else has no recipe here
	/// (the game answers null for a prefab that is not a buildable piece).
	/// </summary>
	private static IReadOnlyList<Cost> RecipeOf(string prefab)
	{
		if (prefab == "wood_floor")
		{
			return Costs(One("$item_wood", 4));
		}
		if (prefab == "wood_wall")
		{
			return Costs(One("$item_wood", 2));
		}
		return null;
	}

	/// <summary>Section (w): the amount of one item in a cost map (0 when it is not there).</summary>
	private static int Amount(Dictionary<string, int> costs, string item)
	{
		int amount;
		return costs != null && costs.TryGetValue(item, out amount) ? amount : 0;
	}

	/// <summary>Section (w): two cost maps hold the same pairs (the key count included).</summary>
	private static bool Same(Dictionary<string, int> a, Dictionary<string, int> b)
	{
		if (a == null || b == null)
		{
			return a == b;
		}
		if (a.Count != b.Count)
		{
			return false;
		}
		foreach (KeyValuePair<string, int> kv in a)
		{
			if (Amount(b, kv.Key) != kv.Value)
			{
				return false;
			}
		}
		return true;
	}

	// ============================================================================================ (x) de onde a peca e alcancada

	/// <summary>
	/// Section (x): <c>BuildWalkSpot</c> - <b>where the companion stands</b> to place a piece that is not at its feet.
	/// Run 13 (04/11/2026) measured it on the ridge of the <c>CommHomeBasicBP</c>: the walk was sent at the piece's own pose
	/// (a roof row at 48,8, ten metres up), the path snap started the route far from the feet and the companion walked the
	/// <b>wrong way</b> - "no progress" after 12 s at <b>22,8 m</b>, then <b>23,5 m</b>. The table asserted here is the
	/// choice of the surface to walk to: the <b>same storey</b> first (2,5 m below to 0,5 m above the piece, the closest one
	/// to the companion wins), then the <b>highest</b> surface the piece can still be reached from (the game's eye rule,
	/// <c>MaxPlace</c> from <c>surface + 1,5 m</c>); none = the old target (the piece itself), so the caller never loses its
	/// walk. The coordinates are exact halves on purpose: the boundary cases (2,5 m and 0,5 m) are about the <b>inclusive</b>
	/// ends, not about a float rounding.
	/// </summary>
	private static void WalkSpotCases()
	{
		UnityEngine.Vector3 me = new UnityEngine.Vector3(0f, 5f, 0f);
		UnityEngine.Vector3 piece = new UnityEngine.Vector3(0f, 10f, 0f);

		// 1) the roof row 1 m under the piece wins over the ground 8 m under it (the same storey is the shortest walk and
		// the only one the plan built the way to)
		WalkCase("telhado 1 m abaixo vence o chao 8 m abaixo", me, piece, 10f,
			new[]
			{
				new UnityEngine.Vector3(0f, 9f, 0f),    // 1 m below the piece
				new UnityEngine.Vector3(0f, 2f, 0f)     // the ground, 8 m below it
			}, true, new UnityEngine.Vector3(0f, 9f, 0f));

		// 2) two surfaces of the same storey: the closest one to the companion (the shorter walk)
		WalkCase("dois do mesmo andar: o mais perto ganha", me, piece, 10f,
			new[]
			{
				new UnityEngine.Vector3(0.8f, 8f, 0f),   // first in the list, farther
				new UnityEngine.Vector3(0f, 8f, 0f)      // the one under the piece
			}, true, new UnityEngine.Vector3(0f, 8f, 0f));

		// 3) nothing of the piece's own storey: the highest floor the eye rule still covers
		WalkCase("so chao dentro do olho: o mais alto ganha", new UnityEngine.Vector3(0f, 0f, 0f), piece, 10f,
			new[]
			{
				new UnityEngine.Vector3(0f, 6f, 0f),    // 4 m below
				new UnityEngine.Vector3(0f, 4f, 0f)     // 6 m below
			}, true, new UnityEngine.Vector3(0f, 6f, 0f));

		// 4) no surface the piece can be reached from (the eye rule, 5 m here): false and the piece itself comes back
		WalkCase("nada dentro do olho: false e a peca volta como alvo", new UnityEngine.Vector3(0f, 0f, 0f), piece, 5f,
			new[]
			{
				new UnityEngine.Vector3(0f, 2f, 0f)     // eye at 3,5: 6,5 m from the piece, out of the 5 m rule
			}, false, piece);

		// 5) a surface 0,5 m ABOVE the piece is still its own storey (the ridge line of a roof, the top of the beam it
		// leans on) - and it wins over a farther same-storey one
		WalkCase("superficie 0,5 m acima da peca e aceita", me, piece, 10f,
			new[]
			{
				new UnityEngine.Vector3(0.2f, 10.5f, 0f),   // 0,5 m above: the inclusive end
				new UnityEngine.Vector3(1f, 8f, 0f)         // 2 m below, farther from the companion
			}, true, new UnityEngine.Vector3(0.2f, 10.5f, 0f));

		// 6) the 2,5 m end of the same storey is INCLUSIVE: exactly 2,5 m below still counts (and wins over a 2 m one that
		// is farther), which is what keeps the row under a 2 m wall
		WalkCase("fronteira de 2,5 m e inclusiva", me, piece, 10f,
			new[]
			{
				new UnityEngine.Vector3(0.1f, 7.5f, 0f),   // exactly 2,5 m below, at the companion's feet
				new UnityEngine.Vector3(2f, 8f, 0f)        // 2 m below, 2 m away
			}, true, new UnityEngine.Vector3(0.1f, 7.5f, 0f));

		// 7) a tie is decided by the coordinates, never by the order the surfaces were collected in: two surfaces 1 m to
		// each side come back as the same point with the list reversed
		UnityEngine.Vector3 right = new UnityEngine.Vector3(1f, 8f, 0f);
		UnityEngine.Vector3 left = new UnityEngine.Vector3(-1f, 8f, 0f);
		UnityEngine.Vector3 first;
		UnityEngine.Vector3 second;
		bool okFirst = BuildWalkSpot.Choose(me, piece, 10f, new List<UnityEngine.Vector3> { right, left }, out first);
		bool okSecond = BuildWalkSpot.Choose(me, piece, 10f, new List<UnityEngine.Vector3> { left, right }, out second);
		Check("empate a 1 m dos dois lados: a lista invertida da o MESMO ponto -> " + Vec(left),
			okFirst && okSecond && Same(first, second) && Same(first, left),
			"1a=" + Vec(first) + ", 2a=" + Vec(second));

		// 8) a surface 2,6 m below is NOT the same storey: it falls to the second pass and is still chosen (it is the only
		// one, and the piece is 1,1 m above its eye)
		WalkCase("chao 2,6 m abaixo cai na 2a passada", me, piece, 10f,
			new[]
			{
				new UnityEngine.Vector3(0f, 7.4f, 0f)   // 2,6 m below: the neighbour of the next storey down
			}, true, new UnityEngine.Vector3(0f, 7.4f, 0f));

		// 9) a piece at the level of a floor is placed from THAT floor, not from the one 3 m under it
		WalkCase("peca no mesmo nivel: o candidato do mesmo nivel", new UnityEngine.Vector3(0f, 9f, 0f), piece, 10f,
			new[]
			{
				new UnityEngine.Vector3(0.5f, 10f, 0f),   // the piece's own level
				new UnityEngine.Vector3(0f, 7f, 0f)       // 3 m below
			}, true, new UnityEngine.Vector3(0.5f, 10f, 0f));

		// 10) no surface at all: false (the caller keeps the piece as its target)
		WalkCase("lista vazia: false e a peca volta como alvo", me, piece, 10f, new UnityEngine.Vector3[0], false, piece);
	}

	/// <summary>Section (x): one <c>Choose</c> case - the surfaces in the given order and the expected answer.</summary>
	private static void WalkCase(string name, UnityEngine.Vector3 me, UnityEngine.Vector3 piece, float maxPlace,
		UnityEngine.Vector3[] surfaces, bool found, UnityEngine.Vector3 expected)
	{
		UnityEngine.Vector3 got;
		bool ok = BuildWalkSpot.Choose(me, piece, maxPlace, new List<UnityEngine.Vector3>(surfaces), out got);
		bool right = ok == found && (!ok || Same(got, expected));
		Check(name + " -> " + (found ? Vec(expected) : "false"), right,
			"obtido " + (ok ? Vec(got) : "false") + ", esperado " + (found ? Vec(expected) : "false"));
	}

	/// <summary>Section (x): the same point (a tenth of a millimetre).</summary>
	private static bool Same(UnityEngine.Vector3 a, UnityEngine.Vector3 b)
	{
		return (a - b).sqrMagnitude < 0.00000001f;
	}

	/// <summary>Section (x): a point for the messages.</summary>
	private static string Vec(UnityEngine.Vector3 v)
	{
		return "(" + v.x.ToString("0.###") + ", " + v.y.ToString("0.###") + ", " + v.z.ToString("0.###") + ")";
	}

	// ============================================================================================ (y) para onde sair da frente

	/// <summary>
	/// Section (y): <c>BuildAsideSpot</c> - <b>where the companion of the site steps to</b> when another one has to place a
	/// piece on its body (the step aside of <c>MHR_Build</c>). Run 14 (04/11/2026) measured the ping-pong of the last roof
	/// row: the step out of piece 909 landed on <b>910</b> (the next piece of the plan), the step out of 910 landed on the
	/// next one, and two companions took turns moving each other around the ridge while the plan did not move.
	///
	/// <para>The table asserted here is the ring <c>Plan</c> returns: away from the placer's front first (the 3 directions
	/// behind it - straight behind and the two 45 degrees), then its two <b>sides</b>, its <b>front never</b> (the plan's
	/// next pieces are there); inside a rank the direction most directly behind wins, then the shortest step; and a spot
	/// <b>1,5 m</b> (XZ) from a <b>pending</b> piece of the same row (within <see cref="BuildAsideSpot.Height"/> - the
	/// body's capsule, 1,8 m - of the piece being placed) is out. All of
	/// them out = <b>no step at all</b> (the caller does not ask and the piece goes to the site's queue). The coordinates
	/// are the exact ring values (2,2 m and 2,2 x cos45) and the tolerance is a centimetre: the cases are about the rule,
	/// not about float rounding.</para>
	/// </summary>
	private static void AsideSpots()
	{
		UnityEngine.Vector3 piece = new UnityEngine.Vector3(0f, 10f, 0f);
		UnityEngine.Vector3 back = new UnityEngine.Vector3(1f, 0f, 0f);   // the placer's BACK: its front is -X
		float diag = 2.2f * 0.70710678f;

		// 1) the plain ring: 3 behind + 2 sides, straight behind first, the front never; the deterministic order is
		// straight behind, the two 45 degrees (z ascending) and the two sides (z ascending)
		AsideCase("anel livre: 5 destinos, o de tras primeiro e a frente nunca", piece, back, new UnityEngine.Vector3[0],
			5, new UnityEngine.Vector3(2.2f, 10f, 0f));
		AsideCase("o 2o e o 3o sao os 45 graus de tras", piece, back, new UnityEngine.Vector3[0], 5,
			new UnityEngine.Vector3(diag, 10f, -diag), 1);
		AsideCase("o 4o e o 5o sao os dois lados", piece, back, new UnityEngine.Vector3[0], 5,
			new UnityEngine.Vector3(0f, 10f, 2.2f), 4);

		// 2) a pending piece where the first candidate lands: it is dropped and the next one comes first (piece 910 of the
		// Run 14 is exactly this - the step out of 909 was on the next piece)
		AsideCase("peça pendente no 1o destino: ele sai da lista", piece, back,
			new[] { new UnityEngine.Vector3(2.2f, 10f, 0f) }, 4, new UnityEngine.Vector3(diag, 10f, -diag));

		// 3) every candidate on a pending piece: nowhere to go (the caller does not ask - the piece goes to the queue)
		UnityEngine.Vector3[] blocked =
		{
			new UnityEngine.Vector3(2.2f, 10f, 0f),
			new UnityEngine.Vector3(diag, 10f, -diag),
			new UnityEngine.Vector3(diag, 10f, diag),
			new UnityEngine.Vector3(0f, 10f, -2.2f),
			new UnityEngine.Vector3(0f, 10f, 2.2f)
		};
		AsideCount("todos os 5 destinos com peça pendente: nenhum destino", piece, back, blocked, 0);

		// 4) a pending piece of ANOTHER storey does not block (a floor 4 m above the piece being placed is not the row)
		AsideCase("peça pendente 4 m acima nao bloqueia", piece, back,
			new[] { new UnityEngine.Vector3(2.2f, 14f, 0f) }, 5, new UnityEngine.Vector3(2.2f, 10f, 0f));

		// 5) the band is Height = 1,8 m - the body's capsule, not the storey - and the edge is INCLUSIVE. It was 2,0 m
		// (04/11/2026, measured: 42 of the 52 pieces blocked by a companion never moved anybody, 81%): the 46,8 tiles of
		// the house against the 48,8 ones are exactly 2,0 m apart, and they took a destination from a companion that was
		// only UNDER them. So 2,1 m above was never the first free height - 2,0 m already is one.
		AsideCase("peça pendente 2,1 m acima ja nao bloqueia", piece, back,
			new[] { new UnityEngine.Vector3(2.2f, 12.1f, 0f) }, 5, new UnityEngine.Vector3(2.2f, 10f, 0f));
		// the exact edge is asserted from the ground: 1,8f is not reachable from y = 10 in a float (11,8f - 10f = 1,80000019
		// against Height = 1,79999995, measured by a probe in this section), so the piece being placed sits at 0 - and the
		// pending one at the LITERAL 1,8f, not at the constant: a band narrowed to 1,5 m would let this piece through (5
		// destinations) and a band of 2,0 m or more is caught by the case below, so the pair pins the number 1,8
		AsideCase("peça pendente a 1,8 m acima ainda bloqueia o destino", UnityEngine.Vector3.zero, back,
			new[] { new UnityEngine.Vector3(2.2f, 1.8f, 0f) }, 4,
			new UnityEngine.Vector3(diag, 0f, -diag), 0, 0f);
		AsideCase("peça pendente a 2,0 m acima não bloqueia mais o destino", piece, back,
			new[] { new UnityEngine.Vector3(2.2f, 12f, 0f) }, 5, new UnityEngine.Vector3(2.2f, 10f, 0f));

		// 6) the 1,5 m clearance is INCLUSIVE at the edge: exactly 1,5 m away is free, 1,4 m is out
		AsideCase("peça pendente a 1,5 m do destino: livre", piece, back,
			new[] { new UnityEngine.Vector3(0.7f, 10f, 0f) }, 5, new UnityEngine.Vector3(2.2f, 10f, 0f));
		AsideCase("peça pendente a 1,4 m do destino: bloqueado", piece, back,
			new[] { new UnityEngine.Vector3(0.8f, 10f, 0f) }, 4, new UnityEngine.Vector3(diag, 10f, -diag));

		// 7) the preference order is about the DIRECTION, not the distance: the companion one metre to the side would
		// rather take the 45 degrees on its side (1,65 m) than the straight-behind step (2,42 m) - but straight behind is
		// the direction most behind the placer, so it still comes first
		AsideCase("a direcao de tras ganha do passo mais curto", new UnityEngine.Vector3(0f, 10f, -1f), back,
			new UnityEngine.Vector3[0], 5, new UnityEngine.Vector3(2.2f, 10f, 0f));

		// 8) same dot (the two 45 degrees, then the two sides): the shorter step comes first - on the +Z side of the piece
		// that is the +Z diagonal and the +Z side, and the coordinates alone would have said the opposite
		AsideCase("mesmo angulo: o 45 graus mais perto ganha", new UnityEngine.Vector3(0f, 10f, 1f), back,
			new UnityEngine.Vector3[0], 5, new UnityEngine.Vector3(diag, 10f, diag), 1);
		AsideCase("mesmo angulo: o lado mais perto ganha", new UnityEngine.Vector3(0f, 10f, 1f), back,
			new UnityEngine.Vector3[0], 5, new UnityEngine.Vector3(0f, 10f, 2.2f), 3);

		// 9) the ring follows the placer's back: facing +X (back = +Z) the first candidate is +Z
		AsideCase("anel medido pelo -yaw do colocador", piece, new UnityEngine.Vector3(0f, 0f, 1f),
			new UnityEngine.Vector3[0], 5, new UnityEngine.Vector3(0f, 10f, 2.2f));

		// 10) without a facing (the placer looking straight up/down) there is no front to avoid: the 8 directions are all
		// candidates and the front is allowed (fail-open - the pending pieces still filter)
		AsideCase("sem frente utilizavel: os 8 destinos, a frente incluida", piece, UnityEngine.Vector3.zero,
			new UnityEngine.Vector3[0], 8, new UnityEngine.Vector3(-2.2f, 10f, 0f));

		// 11) a pending piece that is not a number is left out of the filter (it cannot decide anything)
		AsideCase("peça pendente NaN nao bloqueia", piece, back,
			new[] { new UnityEngine.Vector3(float.NaN, 10f, 0f) }, 5, new UnityEngine.Vector3(2.2f, 10f, 0f));

		// 12) a step of zero length is no step at all
		UnityEngine.Vector3[] spots = new UnityEngine.Vector3[BuildAsideSpot.Ring];
		int none = BuildAsideSpot.Plan(piece, piece, back, new System.Collections.Generic.List<UnityEngine.Vector3>(),
			0f, spots);
		Check("passo de 0 m: nenhum destino", none == 0, "obtido " + none);

		// 13) a short buffer never overflows: it is filled up to its size and the count says how many fit
		UnityEngine.Vector3[] shortBuffer = new UnityEngine.Vector3[2];
		int two = BuildAsideSpot.Plan(piece, piece, back, new System.Collections.Generic.List<UnityEngine.Vector3>(),
			2.2f, shortBuffer);
		Check("buffer de 2: enche 2 e responde 2", two == 2
			&& AsideSame(shortBuffer[0], new UnityEngine.Vector3(2.2f, 10f, 0f)),
			"obtido " + two + " (" + Vec(shortBuffer[0]) + ")");
	}

	/// <summary>Section (y): one <c>Plan</c> case - the expected count and a spot that must be at <paramref name="slot"/>.
	/// The piece being placed is (0, <paramref name="pieceY"/>, 0) here (10 m up unless a case needs the exact band edge
	/// measured from the ground); <paramref name="me"/> is the companion's own feet.</summary>
	private static void AsideCase(string name, UnityEngine.Vector3 me, UnityEngine.Vector3 away,
		UnityEngine.Vector3[] pending, int expected, UnityEngine.Vector3 spot, int slot = 0, float pieceY = 10f)
	{
		UnityEngine.Vector3[] spots = new UnityEngine.Vector3[BuildAsideSpot.Ring];
		int count = BuildAsideSpot.Plan(me, new UnityEngine.Vector3(0f, pieceY, 0f), away,
			new System.Collections.Generic.List<UnityEngine.Vector3>(pending), 2.2f, spots);
		bool ok = count == expected && slot < count && AsideSame(spots[slot], spot);
		Check(name + " -> #" + (slot + 1) + " " + Vec(spot) + " (" + expected + " destinos)", ok,
			"obtido " + count + " destino(s), #" + (slot + 1) + " " + (slot < count ? Vec(spots[slot]) : "-"));
	}

	/// <summary>Section (y): one <c>Plan</c> case that only counts the destinations.</summary>
	private static void AsideCount(string name, UnityEngine.Vector3 me, UnityEngine.Vector3 away,
		UnityEngine.Vector3[] pending, int expected)
	{
		UnityEngine.Vector3[] spots = new UnityEngine.Vector3[BuildAsideSpot.Ring];
		int count = BuildAsideSpot.Plan(me, new UnityEngine.Vector3(0f, 10f, 0f), away,
			new System.Collections.Generic.List<UnityEngine.Vector3>(pending), 2.2f, spots);
		Check(name + " -> " + expected + " destino(s)", count == expected, "obtido " + count);
	}

	/// <summary>Section (y): the same point within a centimetre (the ring is exact up to the float of cos45).</summary>
	private static bool AsideSame(UnityEngine.Vector3 a, UnityEngine.Vector3 b)
	{
		return (a - b).sqrMagnitude < 0.0001f;
	}

	// ============================================================================================ (z) a dispersao dos workers

	/// <summary>
	/// Section (z): <c>BuildSpread</c> - <b>how far apart the crew builds</b> (04/11/2026, Run 14: the player's report
	/// <i>"eles parecem estar disputando pra ver quem coloca a peça primeiro, em vez de trabalhar em equipe"</i>, with 88
	/// <c>corpo na peça</c> and 32 step-aside asks in the log). The plan's consecutive indices are neighbours in space (the
	/// parser orders by storey/class/height and then walks each run as a nearest-neighbour path), so a worker takes the
	/// first piece of a small window of the SAME RUN that is <b>far from what the others are building</b>; the pieces it
	/// walked past keep their turn in the site's queue and come back when that place is free.
	///
	/// <para><c>Pick</c> is the choice inside the window (the first candidate far from every claim, and how many came
	/// before it); <c>Far</c> is the one distance rule (XZ, exactly the distance is free, a candidate that is not a number
	/// is never far, a claim that is not a number is ignored); <c>SameRun</c> is the window's edge (the parser's own keys:
	/// storey key and height with 0,01 of slack, class exactly).</para>
	/// </summary>
	private static void SpreadCases()
	{
		float d = BuildSpread.SpreadDistance;

		// --- Pick: the position of the first candidate far from every claim, and how many it left behind
		SpreadPick("janela vazia: nada a escolher", null, null, d, 0, 0);
		SpreadPick("uma candidata so: ela mesma, nada adiado", new[] { SpreadPoint(0f) }, null, d, 0, 0);
		SpreadPick("minDistance 0: hoje (a primeira, nada adiado)", new[] { SpreadPoint(0f), SpreadPoint(10f) },
			new[] { SpreadPoint(0f) }, 0f, 0, 0);
		SpreadPick("exatamente 3 m de um claim: livre", new[] { SpreadPoint(3f) }, new[] { SpreadPoint(0f) }, d, 0, 0);
		SpreadPick("a 1a em cima de um claim: a 2a entra, 1 adiada", new[] { SpreadPoint(0f), SpreadPoint(10f) },
			new[] { SpreadPoint(0f) }, d, 1, 1);
		SpreadPick("todas perto: a primeira (hoje) e nada adiado", new[] { SpreadPoint(0f), SpreadPoint(0.5f) },
			new[] { SpreadPoint(0f) }, d, 0, 0);
		// the buffer: the window of 6 with the first 5 on claims - the last one is taken with 5 passed, never 6
		SpreadPick("6 candidatas, as 5 primeiras com claim: #6 e 5 adiadas",
			new[] { SpreadPoint(0f), SpreadPoint(10f), SpreadPoint(20f), SpreadPoint(30f), SpreadPoint(40f), SpreadPoint(50f) },
			new[] { SpreadPoint(0f), SpreadPoint(10f), SpreadPoint(20f), SpreadPoint(30f), SpreadPoint(40f) }, d, 5, 5);
		SpreadPick("candidata NaN: a proxima e escolhida",
			new[] { new UnityEngine.Vector3(float.NaN, 40f, 0f), SpreadPoint(50f) }, null, d, 1, 1);
		SpreadPick("claim NaN ignorado: a 1a entra", new[] { SpreadPoint(0f) },
			new[] { new UnityEngine.Vector3(float.NaN, 40f, 0f) }, d, 0, 0);
		SpreadPick("minDistance NaN: hoje, nada adiado", new[] { SpreadPoint(0f), SpreadPoint(10f) },
			new[] { SpreadPoint(0f) }, float.NaN, 0, 0);
		SpreadPick("sem claims: a 1a entra", new[] { SpreadPoint(0f), SpreadPoint(10f) }, null, d, 0, 0);

		// --- Far: the one distance rule (XZ, the edge is free, a broken pose never freezes the site)
		SpreadFar("mesmo XZ 10 m acima: perto", new UnityEngine.Vector3(0f, 50f, 0f), new[] { SpreadPoint(0f) }, d, false);
		SpreadFar("exatamente 3 m: longe", SpreadPoint(3f), new[] { SpreadPoint(0f) }, d, true);
		SpreadFar("minDistance 0: longe", SpreadPoint(0f), new[] { SpreadPoint(0f) }, 0f, true);
		SpreadFar("candidata NaN: nunca longe", new UnityEngine.Vector3(float.NaN, 40f, 0f), new[] { SpreadPoint(0f) },
			d, false);
		SpreadFar("claim NaN ignorado: longe", SpreadPoint(0f), new[] { new UnityEngine.Vector3(float.NaN, 40f, 0f) }, d, true);
		SpreadFar("sem claims: longe", SpreadPoint(0f), null, d, true);

		// --- SameRun: the keys of the parser cut the window (storey key and height 0,01, class exactly)
		SpreadRun("mesma run", 0, 10f, 40f, 0, 10f, 40f, true);
		SpreadRun("Level 0,02 acima: outra run", 0, 10f, 40f, 0, 10.02f, 40f, false);
		SpreadRun("classe diferente: outra run", 0, 10f, 40f, 2, 10f, 40f, false);
		SpreadRun("altura 0,02 acima: outra run", 0, 10f, 40f, 0, 10f, 40.02f, false);
		SpreadRun("altura NaN: outra run", 0, 10f, float.NaN, 0, 10f, 40f, false);
	}

	/// <summary>Section (z): one <c>Pick</c> case - the expected position in the window and how many it left behind.</summary>
	private static void SpreadPick(string name, UnityEngine.Vector3[] window, UnityEngine.Vector3[] claims, float distance,
		int expectedAt, int expectedPassed)
	{
		List<UnityEngine.Vector3> pool = window == null ? null : new List<UnityEngine.Vector3>(window);
		List<UnityEngine.Vector3> known = claims == null ? null : new List<UnityEngine.Vector3>(claims);
		int passed;
		int at = BuildSpread.Pick(pool, known, distance, out passed);
		Check(name + " -> #" + (expectedAt + 1) + ", " + expectedPassed + " adiada(s)",
			at == expectedAt && passed == expectedPassed,
			"obtido #" + (at + 1) + ", " + passed + " adiada(s)");
	}

	/// <summary>Section (z): one <c>Far</c> case - the candidate and the claims the site is building right now.</summary>
	private static void SpreadFar(string name, UnityEngine.Vector3 piece, UnityEngine.Vector3[] claims, float distance,
		bool expected)
	{
		List<UnityEngine.Vector3> known = claims == null ? null : new List<UnityEngine.Vector3>(claims);
		bool far = BuildSpread.Far(piece, known, distance);
		Check(name + " -> " + (expected ? "longe" : "perto"), far == expected, "obtido " + (far ? "longe" : "perto"));
	}

	/// <summary>Section (z): one <c>SameRun</c> case - two pieces of the plan and whether they are the same run.</summary>
	private static void SpreadRun(string name, int classA, float levelA, float yA, int classB, float levelB, float yB,
		bool expected)
	{
		bool same = BuildSpread.SameRun(classA, levelA, yA, classB, levelB, yB);
		Check(name + " -> " + (expected ? "mesma run" : "outra run"), same == expected,
			"obtido " + (same ? "mesma run" : "outra run"));
	}

	/// <summary>Section (z): a pose of the plan - every case is on y = 40 (the spread measures XZ only).</summary>
	private static UnityEngine.Vector3 SpreadPoint(float x, float z = 0f)
	{
		return new UnityEngine.Vector3(x, 40f, z);
	}

	// ============================================================================================ (aa) a peca que eu coloco daqui

	/// <summary>
	/// Section (aa): <c>BuildSpread.Nearest</c> - the <b>nearest pending piece this worker can place from where it stands</b>
	/// (the P0 of the redesign of the build order, 04/11/2026). The rule is one distance and one filter: the smallest
	/// <b>XZ</b> distance to the body among the candidates at most <c>reach</c> away (exactly the reach is inside), one
	/// index skipped and every pose another companion already claimed left out.
	///
	/// <para>Three things separate it from <c>PickNear</c> and are exactly what the table pins: there is <b>no</b>
	/// <c>SpreadDistance</c> of 3 m (a piece at the feet is taken no matter how many companions are around), the limit is
	/// the <b>reach</b> (the caller's rule - the placement itself needs no body next to the piece) and the answer is the
	/// index of the caller's window, never a distance. <b>-1</b> means "walk to today's piece, byte for byte": an empty
	/// list, a reach of zero or less (or one that is not a number) and a body that is not a number all answer it, and a
	/// candidate that is not a number is never chosen. A tie keeps the <b>lower index</b> (the plan's own order), so the
	/// answer never depends on the order the list was filled in.</para>
	/// </summary>
	private static void NearestCases()
	{
		UnityEngine.Vector3 me = SpreadPoint(0f);   // the companion's own feet: (0, 40, 0)

		// 1) the nearest inside the reach wins - 2 / 5 / 9 m of XZ with a reach of 6 is the piece at 2 m
		NearestCase("3 pecas a 2 / 5 / 9 m de XZ, alcance 6", new[] { SpreadPoint(2f), SpreadPoint(5f), SpreadPoint(9f) },
			null, me, 6f, -1, 0);

		// 2) the nearest one is outside the reach: nothing to place from here (the caller walks to today's piece)
		NearestCase("a mais perto fora do alcance (2 e 5 m, alcance 1)", new[] { SpreadPoint(2f), SpreadPoint(5f) },
			null, me, 1f, -1, -1);

		// 3) the edge is INCLUSIVE: exactly 6,0 m is inside, 6,0001 m is not
		NearestCase("borda exata: 6,0 m com alcance 6 (a borda conta)", new[] { SpreadPoint(6f) }, null, me, 6f, -1, 0);
		NearestCase("borda exata: 6,0001 m com alcance 6 (passou da borda)", new[] { SpreadPoint(6.0001f) }, null, me,
			6f, -1, -1);

		// 4) skip is the index of the piece the caller is on: the nearest is left out and the second one is taken
		NearestCase("skip do indice da mais perto: a segunda (5 m)",
			new[] { SpreadPoint(2f), SpreadPoint(5f), SpreadPoint(9f) }, null, me, 6f, 0, 1);

		// 5) -1 skips nothing
		NearestCase("skip -1: nada e ignorado (a de 2 m)", new[] { SpreadPoint(2f), SpreadPoint(5f) }, null, me, 6f, -1, 0);

		// 6) the nearest piece belongs to another companion (it is claimed): the next free one is taken
		NearestCase("a mais perto reivindicada: a proxima livre (5 m)", new[] { SpreadPoint(2f), SpreadPoint(5f) },
			new[] { SpreadPoint(2f) }, me, 6f, -1, 1);

		// 7) a claim of ANOTHER storey is not this piece: the floor and the roof right above it share the XZ (here 2 m
		// apart, far beyond KeySlack = 0,01 m), so the claimed floor must not take the roof away
		NearestCase("reivindicada 2 m acima (piso x telhado): a de baixo e escolhida", new[] { SpreadPoint(2f) },
			new[] { new UnityEngine.Vector3(2f, 42f, 0f) }, me, 6f, -1, 0);

		// 8) fail-open: a pose that is not a number is never chosen, and a body / reach that is not a number (or a reach
		// of zero) answers -1 - the caller keeps today's piece
		NearestCase("pose NaN: a proxima e escolhida (5 m)",
			new[] { new UnityEngine.Vector3(float.NaN, 40f, 0f), SpreadPoint(5f) }, null, me, 6f, -1, 1);
		NearestCase("me (o corpo) NaN: -1", new[] { SpreadPoint(2f) }, null,
			new UnityEngine.Vector3(float.NaN, 40f, 0f), 6f, -1, -1);
		NearestCase("alcance NaN: -1", new[] { SpreadPoint(2f) }, null, me, float.NaN, -1, -1);
		NearestCase("alcance 0: -1", new[] { SpreadPoint(2f) }, null, me, 0f, -1, -1);

		// 9) no candidates at all (an empty window, and a null one: the caller has nothing to place)
		int empty = BuildSpread.Nearest(new List<UnityEngine.Vector3>(), null, me, 6f, -1);
		int noList = BuildSpread.Nearest(null, null, me, 6f, -1);
		Check("Nearest: lista vazia e lista null -> -1", empty == -1 && noList == -1,
			"obtido " + NearestAt(empty) + " / " + NearestAt(noList));

		// 10) an exact tie keeps the lower index - the plan's own order, never the order the list was filled in
		NearestCase("empate exato de distancia: o indice menor", new[] { SpreadPoint(2f), SpreadPoint(-2f) }, null, me,
			6f, -1, 0);

		// and the distance is XZ, not 3D: the piece 7 m UP but 2 m to the side is nearer than the one at the feet's
		// height and 5 m away - in 3D it is the other way round (7,28 m against 5 m, so a 3D rule would take #2)
		NearestCase("a distancia e XZ: peca 7 m acima a 2 m ganha da de 5 m no mesmo nivel",
			new[] { new UnityEngine.Vector3(2f, 47f, 0f), SpreadPoint(5f) }, null, me, 6f, -1, 0);
	}

	/// <summary>Section (aa): one <c>Nearest</c> case - the expected index into the list (-1 = none to place from here).</summary>
	private static void NearestCase(string name, UnityEngine.Vector3[] poses, UnityEngine.Vector3[] claims,
		UnityEngine.Vector3 me, float reach, int skip, int expected)
	{
		List<UnityEngine.Vector3> pool = poses == null ? null : new List<UnityEngine.Vector3>(poses);
		List<UnityEngine.Vector3> known = claims == null ? null : new List<UnityEngine.Vector3>(claims);
		int at = BuildSpread.Nearest(pool, known, me, reach, skip);
		Check("Nearest: " + name + " -> " + NearestAt(expected), at == expected, "obtido " + NearestAt(at));
	}

	/// <summary>Section (aa): the index as the messages print it - the plan's position (#1) or the -1 of "none".</summary>
	private static string NearestAt(int at)
	{
		return at < 0 ? "-1 (nenhuma)" : "#" + (at + 1);
	}

	// ============================================================================================ (ab) o trecho de cada um

	/// <summary>
	/// Section (ab): <c>BuildSpread.Chunk</c> - <b>the stretch of the plan one worker owns</b> (04/11/2026, the player's rule
	/// <i>"cada companheiro é dono de um trecho, não negocia peça"</i>). The plan is a nearest-neighbour tour inside each run
	/// (<c>BuildBlueprintParser.Ordered</c>), so cutting the run <b>by index</b> cuts the site into contiguous stretches and
	/// the contention of the crew is gone by construction: the run that measured the repulsion rose to 125 pieces/min and
	/// <b>decayed to 107</b> at the end, when more workers were reaching for the same corner.
	///
	/// <para>Three properties are pinned here and they are the whole contract: the chunks <b>cover</b> the phase exactly once
	/// (no piece twice, no piece lost, in order), two chunks never differ by more than <b>1</b> piece (the remainder goes to
	/// the first ones) and the fail-safe for a phase or a crew number that makes no sense is an <b>empty</b> chunk starting at
	/// the phase's first index - never a piece of somebody else.</para>
	/// </summary>
	private static void ChunkCases()
	{
		// 1-3) one and two workers over a phase of 10: half each, in order
		ChunkCase("10 pecas, 1 worker: a fase inteira", 0, 10, 1, 0, 0, 10);
		ChunkCase("10 pecas, 2 workers: o primeiro leva 5", 0, 10, 2, 0, 0, 5);
		ChunkCase("10 pecas, 2 workers: o segundo leva 5", 0, 10, 2, 1, 5, 5);

		// 4-6) the remainder goes to the FIRST chunks: 10 over 3 is 4 + 3 + 3, never 3 + 3 + 4
		ChunkCase("10 pecas, 3 workers: o primeiro leva o resto (4)", 0, 10, 3, 0, 0, 4);
		ChunkCase("10 pecas, 3 workers: o segundo leva 3", 0, 10, 3, 1, 4, 3);
		ChunkCase("10 pecas, 3 workers: o terceiro leva 3", 0, 10, 3, 2, 7, 3);

		// 7-9) 7 over 3 is 3 + 2 + 2 (the remainder is 1 and only the first chunk takes it)
		ChunkCase("7 pecas, 3 workers: o primeiro leva 3", 0, 7, 3, 0, 0, 3);
		ChunkCase("7 pecas, 3 workers: o segundo leva 2", 0, 7, 3, 1, 3, 2);
		ChunkCase("7 pecas, 3 workers: o terceiro leva 2", 0, 7, 3, 2, 5, 2);

		// 10-11) a phase of 100 over 5 workers, and the offset of a phase that does not start at 0
		ChunkCase("100 pecas, 5 workers, fase a partir de 100: o primeiro", 100, 200, 5, 0, 100, 20);
		ChunkCase("100 pecas, 5 workers: o ultimo comeca em 180", 100, 200, 5, 4, 180, 20);

		// 12-13) fewer pieces than workers: the first chunks take one each and the tail ones are EMPTY (never a copy)
		ChunkCase("1 peca, 5 workers: o primeiro fica com ela", 100, 101, 5, 0, 100, 1);
		ChunkCase("1 peca, 5 workers: o ultimo fica vazio no fim", 100, 101, 5, 4, 101, 0);

		// 14-15) an empty phase and a phase that ends before it starts: empty, never a negative length
		ChunkCase("fase vazia (start == end): nada", 100, 100, 3, 0, 100, 0);
		ChunkCase("fase invertida (end < start): nada", 100, 90, 3, 0, 100, 0);

		// 16-20) the fail-safe of the numbers that make no sense: 0 slots, a negative count, a slot out of range
		ChunkCase("0 workers: nada", 0, 10, 0, 0, 0, 0);
		ChunkCase("-3 workers: nada", 0, 10, -3, 0, 0, 0);
		ChunkCase("slot -1: nada", 0, 10, 3, -1, 0, 0);
		ChunkCase("slot == slots: nada", 0, 10, 3, 3, 0, 0);
		ChunkCase("slot > slots: nada", 0, 10, 3, 4, 0, 0);

		// 21) the property over the whole small table: the chunks cover [start, end) exactly once and in order
		bool covered = true;
		string broken = null;
		for (int pieces = 2; pieces <= 12 && covered; pieces++)
		{
			for (int slots = 1; slots <= 6 && covered; slots++)
			{
				int at = 100;
				for (int slot = 0; slot < slots; slot++)
				{
					BuildSpread.Chunk(100, 100 + pieces, slots, slot, out int first, out int length);
					if (first != at || length < 0)
					{
						covered = false;
						broken = pieces + " peca(s), " + slots + " worker(s), slot " + slot + ": " + first + "+" + length
							+ " (esperado " + at + ")";
						break;
					}
					at += length;
				}
				if (covered && at != 100 + pieces)
				{
					covered = false;
					broken = pieces + " peca(s), " + slots + " worker(s): cobriu ate " + at;
				}
			}
		}
		Check("2..12 pecas x 1..6 workers: os trechos cobrem a fase exatamente uma vez, em ordem", covered, broken);

		// 22) and they never differ by more than 1 piece - the same sweep, the largest minus the smallest
		bool even = true;
		string uneven = null;
		for (int pieces = 2; pieces <= 12 && even; pieces++)
		{
			for (int slots = 1; slots <= 6 && even; slots++)
			{
				int min = int.MaxValue;
				int max = int.MinValue;
				for (int slot = 0; slot < slots; slot++)
				{
					BuildSpread.Chunk(100, 100 + pieces, slots, slot, out int first, out int length);
					if (length < min)
					{
						min = length;
					}
					if (length > max)
					{
						max = length;
					}
				}
				if (max - min > 1)
				{
					even = false;
					uneven = pieces + " peca(s), " + slots + " worker(s): " + min + ".." + max;
				}
			}
		}
		Check("2..12 pecas x 1..6 workers: o maior trecho e no maximo 1 peca maior que o menor", even, uneven);
	}

	/// <summary>Section (ab): one <c>Chunk</c> case - the phase, the crew, the slot and the expected first index/length.</summary>
	private static void ChunkCase(string name, int start, int end, int slots, int slot, int expectedFirst, int expectedLength)
	{
		BuildSpread.Chunk(start, end, slots, slot, out int first, out int length);
		Check(name + " -> " + expectedFirst + "+" + expectedLength, first == expectedFirst && length == expectedLength,
			"obtido " + first + "+" + length);
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
