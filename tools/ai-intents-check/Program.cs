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
/// CompanionNames,MHR_Owner}.cs and src/Companions/Core/MHR_List.cs straight into this executable, so every rule
/// asserted here is the shipped one (Stubs.cs holds only the Unity/config members those files touch).
///
/// <para>Two of those members cannot be linked (they live in classes that need the world), so they are copies in
/// Stubs.cs: <c>CompanionOrders.IsDoorRequest</c> and <c>CompanionOrders.JobMode</c>. Section (h) loads the built
/// Companions.dll and compares the copies with the real methods - the harness fails if they ever drift.</para>
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
	private static void Templates()
	{
		// the names, in the five languages
		TemplateCase("constrói uma cabana aqui", "hut");
		TemplateCase("constrói um abrigo para a bancada", "canopy");
		TemplateCase("abrigo da bancada", "canopy");
		TemplateCase("build a small hut", "hut");
		TemplateCase("a shelter for the bench", "canopy");
		TemplateCase("construye una choza", "hut");
		TemplateCase("un cobertizo", "canopy");
		TemplateCase("construis une cabane", "hut");
		TemplateCase("un auvent", "canopy");
		TemplateCase("costruisci una casetta", "hut");
		TemplateCase("un riparo", "canopy");
		Check("Resolve(\"constrói uma parede\") == null (template nao e peca)", BuildTemplates.Resolve("constrói uma parede") == null);
		Check("Resolve(null) == null", BuildTemplates.Resolve(null) == null);
		// "cabana" nao pode ser lida como a peca "cama" (o catalogo da E5 nao conhece cabana)
		Check("BuildCatalog.Resolve(\"cabana\") == null (nao e peca)", BuildCatalog.Resolve("uma cabana") == null);
		Check("BuildCatalog.Resolve(\"cama\") == \"bed\"", BuildCatalog.Resolve("uma cama") == "bed");

		BuildTemplates.Template hut = BuildTemplates.ByName("hut");
		BuildTemplates.Template canopy = BuildTemplates.ByName("canopy");
		Check("ByName(\"hut\")", hut != null);
		Check("ByName(\"canopy\")", canopy != null);
		Check("ByName(\"xyz\") == null", BuildTemplates.ByName("xyz") == null);

		// a composicao da cabana: 1 piso + 4 paredes + 2 telhados (o aceite da E6)
		CheckInt("cabana: 7 pecas", 7, hut.Steps.Length);
		CheckInt("cabana: 1 piso", 1, CountPrefab(hut, "wood_floor"));
		CheckInt("cabana: 4 paredes", 4, CountPrefab(hut, "woodwall"));
		CheckInt("cabana: 2 telhados", 2, CountPrefab(hut, "wood_roof_45"));
		CheckInt("abrigo: 8 pecas", 8, canopy.Steps.Length);
		CheckInt("abrigo: 4 postes", 4, CountPrefab(canopy, "wood_pole2"));
		CheckInt("abrigo: 4 telhados", 4, CountPrefab(canopy, "wood_roof_45"));

		// a ordem de construcao: fundacao -> parede -> telhado
		System.Collections.Generic.List<BuildTemplates.Step> ordered = hut.Ordered();
		CheckText("cabana: a 1a peca e o piso", "wood_floor", ordered[0].Prefab);
		CheckInt("cabana: o piso e nivel 0", 0, ordered[0].Level);
		CheckText("cabana: a ultima peca e o telhado", "wood_roof_45", ordered[ordered.Count - 1].Prefab);
		CheckInt("cabana: o telhado e nivel 2", 2, ordered[ordered.Count - 1].Level);
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
		CheckFloat("telhado a 3 m", 3f, ordered[ordered.Count - 1].Y);
		CheckFloat("telhado com 1 m de beiral", 1f, Math.Abs(ordered[ordered.Count - 1].Z));

		// a allow-list dos planos
		foreach (string prefab in BuildTemplates.Prefabs())
		{
			Check("Allowed(\"" + prefab + "\")", BuildTemplates.Allowed(prefab));
		}
		Check("Allowed(\"piece_workbench\") == false (nao e peca de template)", !BuildTemplates.Allowed("piece_workbench"));
		Check("Allowed(null) == false", !BuildTemplates.Allowed(null));
		CheckInt("Prefabs() sem repeticao", 4, BuildTemplates.Prefabs().Length);
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
