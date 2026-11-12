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
/// The word nets of the AI layer, checked <b>outside the game</b> against the real sources.
///
/// <para><b>The file list lives in <c>ai-intents-check.csproj</c>, not in this comment</b> (11/11/2026). The
/// <c>&lt;Compile Include&gt;</c> items there are what actually compiles, so they are the single source: this summary
/// said "16 files" and was missing the six newest <c>Build*</c> modules while the csproj linked them - a list in prose
/// cannot stay right on its own. What the comment keeps is the <b>shape</b>: the pure files of <c>src/Companions/AI/</c>
/// and <c>Core/MHR_List.cs</c> are compiled straight into this executable (nothing is copied, nothing is
/// reimplemented), and <c>Stubs.cs</c> holds only the Unity/config members those files touch.</para>
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
/// (Run 13, 11/11/2026 - the ridge of the <c>CommHomeBasicBP</c> walked the wrong way). The same storey first, then the
/// highest surface the game's eye rule still covers; none = the piece itself, the old target.</para>
///
/// <para>Section (z) is <c>BuildSpread</c>: <b>how far apart the crew of a site builds</b> (Run 14, 11/11/2026 - the player
/// measured five companions racing for the same corner: 88 <c>corpo na peça</c> and 32 step-aside asks). A shared plan is
/// not walked in a line: a worker takes the first piece of a small window of the same run that is far from what the others
/// are building, and the pieces it walked past keep their turn in the site's queue.</para>
///
/// <para>Run: <c>dotnet run --project tools/ai-intents-check</c> (after <c>dotnet build Companions.sln -c Release</c>,
/// which the cross-check reads). Exit code 0 only when every case passed and nothing was skipped.</para>
///
/// <para><b>Arguments</b> (11/11/2026) - all optional, and <b>no argument means exactly what it meant before</b> (same
/// assertions, same count, same exit code):
/// <c>--list</c> prints the sections in the order they run and stops; <c>--only=(a),(p)</c> rules on the listed
/// sections only (the others still <b>run</b> - they cost microseconds and skipping them would change the state the
/// later ones read, so the filter is on what gets <b>counted and printed</b>); <c>--quiet</c> prints only the FAILs and
/// the summary; <c>--strict</c> turns a stale DLL into a FAIL (see section (h)). The reason for <c>--only</c> is reading
/// the output, not time: the whole run is ~10 s.</para>
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

	/// <summary>11/11/2026: the release gate. Today it only decides what a <b>stale DLL</b> means in section (h) - a note
	/// by default (a <c>git checkout</c> rewrites mtimes and would cry wolf), a FAIL here.</summary>
	private static bool _strict;

	/// <summary>11/11/2026 (<c>--quiet</c>): only the FAILs and the summary are printed.</summary>
	private static bool _quiet;

	/// <summary>11/11/2026 (<c>--list</c>): print the section headers in run order and stop before anything runs.</summary>
	private static bool _list;

	/// <summary>11/11/2026 (<c>--only=(a),(p)</c>): the sections whose cases are <b>counted and printed</b>. Null = all.
	/// The other sections still run - see the summary of the class for why the filter is not a skip.</summary>
	private static HashSet<string> _only;

	/// <summary>The key of the section currently running (<c>a</c>, <c>p</c>, <c>ab</c>...), or null outside one.</summary>
	private static string _current;

	/// <summary>
	/// The sections, in the order they run - <b>the one place</b> that list exists (11/11/2026). <see cref="Main"/>
	/// executes it and <c>--list</c> prints it, which is what keeps the printed order honest. (Before this, the run
	/// order was written inline and the cross-check section printed its own header from inside its body, so it showed up
	/// as (h) <b>after</b> (z) - the number and the position disagreed.)
	///
	/// <para>(h) is the cross-check against the DLL and sits where its number says, between (g) and (i); (ae) is last
	/// because it reads the log counters of every section above it (that is why (af) sits before it, not after).</para>
	/// </summary>
	private static readonly SectionDef[] SectionOrder = new SectionDef[]
	{
		new SectionDef("0", "pre-condicoes: o harness roda FORA do jogo", Preconditions),
		new SectionDef("a", "RadialIntent.Correct - postura, vagar, comando, trabalho e as guardas", Radial),
		new SectionDef("b", "PickupIntent.NeighborAction - nopickup que o jogador desmente", Pickup),
		new SectionDef("c", "GatherIntent.FromModelAction - coleta com numero", Gather),
		new SectionDef("d", "InteractionIntent.Correct - colmeia, porta, navio, sentar, bau", Interaction),
		new SectionDef("e", "LlmIntent.Parse / ExtractContent - o JSON do modelo", Parser),
		new SectionDef("f", "OrderItems.Resolve / GatherAction - o item da ordem nos 5 idiomas", Items),
		new SectionDef("g", "CompanionNameMatch - quem o jogador chamou (bonus)", Names),
		new SectionDef("h", "as copias do Stubs.cs contra a DLL real (Companions.dll)", CrossCheckDoorRule),
		new SectionDef("i", "BuildCatalog - o nome da peca vira o prefab (E5: oficina)", Build),
		new SectionDef("j", "BuildTemplates - a estrutura por template (E6: cabana e abrigo)", Templates),
		new SectionDef("k", "LevelIntent - a ordem de nivelar o terreno (E10)", Leveling),
		new SectionDef("l", "BuildBlueprintParser - o .blueprint do PlanBuild (E7)", Blueprints),
		new SectionDef("m", "BuildBlueprintParser.NearestWithin - a ordem dentro do grupo (vizinho mais proximo)", BlueprintOrder),
		new SectionDef("n", "BuildBlueprintParser.Ordered - a chave do telhado (andar da laje que cobre)", BlueprintRoofKey),
		new SectionDef("o", "NavTileMath.Axis - o tile da navmesh (P2 da navegacao)", NavTiles),
		new SectionDef("p", "BuildFailure.Classify - o portao de apoio da obra (dependencia x falha real)", BuildFailures),
		new SectionDef("q", "BuildPieceKind.Classify - estrutura x mobilia (quanto a peca pode sair do plano)", PieceKinds),
		new SectionDef("r", "BuildSupportBelow - o apoio direto de uma peca (quem o grupo puxa para a frente)", SupportBelow),
		new SectionDef("s", "BuildBody - a janela do corpo na colocacao (blocked_by_player)", BodyWindows),
		new SectionDef("t", "a barreira de andar - recusa por corpo (IsTransient/ShortRetry) e o nivel da peca", BodyBarrier),
		new SectionDef("u", "BuildSupportBelow.FallbackPostAllowed - o poste quando o apoio do plano nao vem", FallbackPosts),
		new SectionDef("v", "BuildStationCover.Required - a bancada exigida so fora do blueprint", StationsCover),
		new SectionDef("w", "BuildMaterials - o custo do plano (necessidade de materiais)", Materials),
		new SectionDef("x", "BuildWalkSpot.Choose - de onde a peca e alcancada (o ponto do walk, Run 13)", WalkSpotCases),
		new SectionDef("y", "BuildAsideSpot.Plan - para onde o companheiro sai da frente (o StepAside do Run 14)", AsideSpots),
		new SectionDef("z", "BuildSpread - a dispersao dos workers (ninguem disputa a peca, Run 14)", SpreadCases),
		new SectionDef("aa", "CompanionChatAI - as 37 acoes do schema sao ensinadas nos 5 contratos", ContractCoverage),
		new SectionDef("ab", "LlmIntent.Parse - toda acao do schema sobrevive ao Canonicalize", CanonicalRoundTrip),
		new SectionDef("ac", "BuildBlueprintSkip - por que uma linha do .blueprint nao entra no plano", BlueprintSkips),
		new SectionDef("ad", "LlmProviders - o dropdown do painel casa a URL com o provedor", ProviderTable),
		new SectionDef("af", "LevelResume - qual fim do nivelamento devolve o canteiro a obra", LevelResumes),
		new SectionDef("ag", "BuildPlanOrder - o reordenamento do plano remapeia os indices (a identidade da peca)", PlanOrders),
		new SectionDef("ah", "ClimbRule - o degrau que o companheiro escalou (a saida do cercado)", ClimbRules),
		new SectionDef("ae", "o log das redes ficou calado - nenhuma regra engoliu excecao", QuietLog)
	};

	/// <summary>One line of <see cref="SectionOrder"/>: the key, the title the header shows, and the cases.</summary>
	private sealed class SectionDef
	{
		internal readonly string Key;
		internal readonly string Title;
		internal readonly Action Body;

		internal SectionDef(string key, string title, Action body)
		{
			Key = key;
			Title = title;
			Body = body;
		}
	}

	/// <summary>Is a case in the running section counted? (True when there is no filter, or the section is in it.)</summary>
	private static bool Counted
	{
		get { return _only == null || (_current != null && _only.Contains(_current)); }
	}

	private static int Main(string[] args)
	{
		_strict = Array.IndexOf(args, "--strict") >= 0;
		_quiet = Array.IndexOf(args, "--quiet") >= 0;
		_list = Array.IndexOf(args, "--list") >= 0;
		string only = ArgumentValue(args, "--only=");
		if (only != null)
		{
			_only = new HashSet<string>(StringComparer.Ordinal);
			foreach (string part in only.Split(new char[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
			{
				// "(a)", "a", "(aa)" and "aa" all mean the same section
				_only.Add(part.Trim().Trim('(', ')').ToLowerInvariant());
			}
		}
		try
		{
			Out.OutputEncoding = Encoding.UTF8;
		}
		catch
		{
			// redirected output without a console: the text is ASCII-safe enough
		}

		InstallAssemblyProbe();

		if (_list)
		{
			PrintSectionList();
			return 0;
		}

		// a typo in --only must not read as a green run: "(zz)" would count nothing and exit 0
		if (_only != null)
		{
			List<string> unknown = new List<string>();
			foreach (string key in Sorted(_only))
			{
				if (!IsSectionKey(key))
				{
					unknown.Add(key);
				}
			}
			if (unknown.Count > 0)
			{
				Out.WriteLine("--only: secao(oes) que nao existem: (" + string.Join("), (", unknown) + ")");
				Out.WriteLine("  as secoes sao as de --list");
				return 2;
			}
		}

		Out.WriteLine("AI Companions - harness das redes por palavras (net472, fontes reais de src/)");
		Out.WriteLine("pastas do jogo: " + (ProbeDirs.Count == 0 ? "(nenhuma encontrada)" : string.Join(" | ", ProbeDirs)));
		if (_only != null)
		{
			Out.WriteLine("filtro --only: so " + (_only.Count == 0 ? "(nenhuma secao)" : "(" + string.Join("), (", Sorted(_only)) + ")")
				+ " e contada (todas rodam)");
		}

		for (int i = 0; i < SectionOrder.Length; i++)
		{
			RunSection(SectionOrder[i].Key, SectionOrder[i].Title, SectionOrder[i].Body);
			if (i == 0 && _fail > 0)
			{
				// (0) is the environment: if it fails the rest of the run means nothing
				Out.WriteLine();
				Out.WriteLine("FATAL: o ambiente do harness esta errado (veja acima). As assemblies do jogo sao procuradas em: "
					+ (ProbeDirs.Count == 0 ? "(nenhuma pasta)" : string.Join(" | ", ProbeDirs.ToArray())));
				return 2;
			}
		}

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
		// The bug of 1.9.6: "level" is in CompanionChatAI.ValidActions (the schema's enum), so the model answers it -
		// and Canonicalize only returns actions its Aliases table knows, so the correct answer came back as "none"
		// and the LevelIntent net rescued it, logging that the MODEL had answered "none" (it had not). Section (ab)
		// makes this structural: every ValidAction has to survive Parse.
		CheckText("Parse(acao \"level\") sobrevive", "level", LlmIntent.Parse("{\"action\":\"level\"}").Action);
		CheckText("Parse(acao \"build\") sobrevive", "build", LlmIntent.Parse("{\"action\":\"build\"}").Action);
		CheckText("Parse(\"nivele o terreno\") -> level", "level", LlmIntent.Parse("{\"action\":\"nivele\"}").Action);
		CheckText("Parse(\"flatten\") -> level", "level", LlmIntent.Parse("{\"action\":\"flatten\"}").Action);
		CheckText("Parse(\"livellare\") -> level", "level", LlmIntent.Parse("{\"action\":\"livellare\"}").Action);
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

	/// <summary>
	/// Section (f): the item an order named. Every case runs inside a <b>list fixture the section sets itself</b>.
	///
	/// <para>The four gather lists are configuration, and the harness's <c>ModConfig</c> stub exists only so the type
	/// has somewhere to read from. Until 11/11/2026 the case "the job's own list is the last place looked at" read the
	/// factory text copied into <c>Stubs.cs</c> - so the section proved the <b>copy</b>, not the rule, and a default
	/// changed in <c>ModConfig.cs</c> would have made it fail for the wrong reason (or, worse, pass for one). The
	/// values below are now the scenario, written here on purpose; the stub's defaults cannot reach any assertion.
	/// The acceptance for that is destructive: replace the factory text in <c>Stubs.cs</c> with garbage and this
	/// section stays green.</para>
	/// </summary>
	private static void Items()
	{
		WithLists(
			"Wood, FineWood, RoundLog, ElderBark, YggdrasilWood, Blackwood, Resin",
			"Stone, Flint, BlackMarble, Grausten",
			"CopperOre, TinOre, IronScrap, SilverOre, BlackMetalScrap, CopperScrap, FlametalOreNew, Obsidian",
			"Raspberry,Blueberries,Cloudberry,Mushroom,MushroomYellow,MushroomBlue,MushroomMagecap,MushroomJotunPuffs,"
				+ "MushroomSmokePuff,Thistle,Dandelion,Carrot,Turnip,Onion,Barley,Flax,RoyalJelly,Honey",
			ItemsOnFixture);
	}

	private static void ItemsOnFixture()
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

		// the job's own list is the last place looked at: Cloudberry is in no alias table, so the only way to get it
		// back is the stone list naming it
		WithLists(null, "Stone, Cloudberry", null, null, delegate
		{
			ItemCase("stone", "cloudberry", 2, "Cloudberry");
		});

		// and the precedence, with a stone list that does NOT name Stone ("Flint" only):
		WithLists(null, "Flint", null, null, delegate
		{
			ItemCase("stone", "pedra", 0, "Stone");     // the alias map decides, the list is not consulted
			ItemCase("stone", "cloudberry", 2, null);   // neither the alias map nor the list knows it
		});

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
		// 11/11/2026 (the storey barrier): a body on the spot is its own class - nobody's fault, no attempt spent. The
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
		// 11/11/2026 (the storey barrier): the yes/no of the body class, in both directions
		Check("IsBlocked(\"blocked_by_player\", true) -> true (corpo no caminho: nenhuma tentativa gasta)",
			BuildFailure.IsBlocked("blocked_by_player", true), "obtido false");
		Check("IsBlocked(\"no_support\", true) -> false (o apoio que falta nao e um corpo)",
			!BuildFailure.IsBlocked("no_support", true), "obtido true");
		// the numbers of the storey barrier, so a silent change of the window is caught here
		// 11/11/2026 (Run 17): one clock - the patience is the same 10 s as BodyBarrierSeconds (it used to be 25 s, so the
		// wait outlived the site's gate and the workers stood still)
		Check("constantes da barreira: 2 s de volta, 1 m de andar, 10 s de paciencia (o mesmo relogio da barreira)",
			Math.Abs(BuildFailure.BodyRetryDelay - 2f) < 0.001f && Math.Abs(BuildFailure.BodyRetryStep - 1f) < 0.001f
				&& Math.Abs(BuildFailure.BodyRetryMaxWait - 10f) < 0.001f,
			"obtido " + BuildFailure.BodyRetryDelay.ToString("F2") + " / " + BuildFailure.BodyRetryStep.ToString("F2")
				+ " / " + BuildFailure.BodyRetryMaxWait.ToString("F2"));

		// 11/11/2026 (the cut of the storey barrier): does the site hold a worker for a piece whose clock says dueAt?
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
		// the surface has to HOLD, not just exist (11/11/2026: 95 refusals of the post itself, `- no_support`, because it was
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
	/// P1 (04/11/2026) and 11/11/2026 (Run 10): the <b>body window</b> of the placement check
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

	/// <summary>11/11/2026 (Run 10): one case of the body window - the expected answer with the numbers
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
	/// 11/11/2026 (the <b>body barrier</b>, the player's rule after the Run 9: "they may skip, but come back and try again
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
		// 11/11/2026 (Run 17): one clock - 10 s is the 3 short retries (7,5 s) rounded up, and ServeBarrier never outlives
		// the site's gate (it used to be 20 s of gate against 25 s of wait)
		Check("constantes: 3 curtas, 2,5 s de espera, 1,5 m de mesmo andar, 10 s de barreira",
			BuildFailure.MaxShortRetries == 3 && Math.Abs(BuildFailure.ShortRetryDelay - 2.5f) < 0.001f
				&& Math.Abs(BuildFailure.BodySameStorey - 1.5f) < 0.001f
				&& Math.Abs(BuildFailure.BodyBarrierSeconds - 10f) < 0.001f,
			"obtido " + BuildFailure.MaxShortRetries + " / " + BuildFailure.ShortRetryDelay.ToString("F2") + " / "
				+ BuildFailure.BodySameStorey.ToString("F2") + " / " + BuildFailure.BodyBarrierSeconds.ToString("F2"));

		// 11/11/2026 (Run 17): the wait of a piece refused by a body grows with the refusals and stops at 6 s - the piece
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
	/// 11/11/2026 (the Run 9: the <c>wood_floor</c> tile at 39,8 was refused by a body, went to the site's queue and the guard
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
	/// Section (v): lote 1 (player's request, 11/11/2026) - the workbench/stonecutter requirement lifted <b>only</b> for a
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
	/// Run 13 (11/11/2026) measured it on the ridge of the <c>CommHomeBasicBP</c>: the walk was sent at the piece's own pose
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
	/// piece on its body (the step aside of <c>MHR_Build</c>). Run 14 (11/11/2026) measured the ping-pong of the last roof
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
		// (11/11/2026, measured: 42 of the 52 pieces blocked by a companion never moved anybody, 81%): the 46,8 tiles of
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
	/// Section (z): <c>BuildSpread</c> - <b>how far apart the crew builds</b> (11/11/2026, Run 14: the player's report
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

		// E10 (11/11/2026): o NOME que o jogador ouve, no idioma do JOGO. Ele sai em duas linhas que o jogador le e ouve:
		// a confirmacao ("Ok: build the 4x4 house") e a fala de conclusao ("The 4x4 house is up: 21 pieces"). O caso
		// real: num jogo em ingles a confirmacao saiu "Ok: build the casa 4x4" - metade ingles (a chave) e metade
		// portugues (este rotulo, que era uma unica string PT). Por isso a prova que importa e: o ingles nunca mostra o
		// nome portugues, e o log continua igual em qualquer idioma (ele usa o Name ASCII, nao o rotulo falado).
		LanguageLabels();

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

	/// <summary>
	/// E10: the name of each template in the five languages, as the player HEARS it.
	///
	/// <para>The label goes into two spoken lines: the confirmation (<c>hc_ai_order_build_plan</c> ->
	/// "Ok: build the {0}") and the completion (<c>hc_ai_build_plan_done</c> -> "The {0} is up: {1} pieces"). The
	/// measured bug: in an English game the confirmation said <b>"Ok: build the casa 4x4"</b>. The label was one PT
	/// string, so it was right in at most one of the five languages the mod ships.</para>
	///
	/// <para>What is asserted, and why each one earns its place:</para>
	/// <list type="bullet">
	/// <item>the exact name per language - pins the table, including the two "false friends" (ES/IT <c>casa</c> is the
	/// same word as the PT <c>casa</c> but is the <i>4x4</i> house there and the plain one here). The English column is
	/// the one that ends the reported bug: nothing there may be a Portuguese word;</item>
	/// <item>an unknown code (<c>de-de</c>, a German game) falls back to English, exactly like the persona and the voice
	/// table of <c>CompanionLocale</c> do;</item>
	/// <item><c>Name</c> is the ASCII key the logs use and does not move with the language, so a run read in another
	/// language stays comparable.</item>
	/// </list>
	/// </summary>
	private static void LanguageLabels()
	{
		// the table: name -> (pt, en, es, fr, it)
		string[][] table = new string[][]
		{
			new[] { "hut", "cabana", "hut", "choza", "cabane", "capanna" },
			new[] { "canopy", "abrigo da bancada", "bench shelter", "cobertizo de la mesa", "auvent pour l'établi", "riparo del banco" },
			new[] { "house", "casa", "house", "casa", "maison", "casa" },
			new[] { "house6", "casa 6x6", "6x6 house", "casa 6x6", "maison 6x6", "casa 6x6" },
			new[] { "house4", "casa 4x4", "4x4 house", "casa 4x4", "maison 4x4", "casa 4x4" },
			new[] { "tent", "tenda", "tent", "tienda", "tente", "tenda" }
		};
		string[] codes = new string[] { "pt-br", "en-us", "es", "fr", "it" };

		string original = CompanionLocale.Current.KokoroCode;
		try
		{
			for (int t = 0; t < table.Length; t++)
			{
				string name = table[t][0];
				BuildTemplates.Template template = BuildTemplates.ByName(name);
				if (template == null)
				{
					Fail("Label(" + name + ")", "o template nao existe");
					continue;
				}
				for (int i = 0; i < codes.Length; i++)
				{
					CompanionLocale.Current.KokoroCode = codes[i];
					CheckText("Label(" + name + ") em " + codes[i], table[t][i + 1], template.Label);
				}

				// idioma desconhecido (jogo em alemao, russo...) cai no ingles, como a persona e a voz
				CompanionLocale.Current.KokoroCode = "de-de";
				CheckText("Label(" + name + ") com codigo desconhecido -> ingles", template.Label, table[t][2]);

				// o log nao anda com o idioma: a ordem e nomeada pelo Name ASCII ("house4"), nao pelo rotulo falado
				CompanionLocale.Current.KokoroCode = "pt-br";
				CheckText("Label(" + name + ") acompanha Name (ASCII)", name, template.Name);
			}
		}
		finally
		{
			// a secao (j) e o ultimo lugar que mexe no idioma: devolver o default real para as que vierem depois
			CompanionLocale.Current.KokoroCode = original;
		}
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
	/// <summary>
	/// Section (aa): every action the JSON schema lets the model answer with (<c>CompanionChatAI.ValidActions</c>, the
	/// single source of the schema's "enum") has to be TAUGHT in each of the five prompt contracts - the list the model
	/// reads first, and the rules/examples below it.
	///
	/// <para>This is the assertion that would have caught the <c>level</c> bug of 1.9.6: the action was in
	/// <c>ValidActions</c> (so the schema offered it) and in the PT contract only, so in EN/ES/FR/IT the model had no
	/// reason to ever answer "level" - it answered "build" with the size as the target ("level the ground 8x8 here" ->
	/// <c>{"action":"build","target":"8x8"}</c>, spoken as "I don't know a build called 8x8"). Whole-word,
	/// case-insensitive: "level" must not be satisfied by "levels" or by another word's substring.</para>
	///
	/// <para>The contracts are read from the built Companions.dll by reflection (the same load (h) does), because
	/// CompanionChatAI.cs cannot be linked here: it needs Unity, the network and the game. An action counts as taught
	/// when its word is in the contract's <b>prose</b> - the text with every <c>{...}</c> example removed. That is
	/// stricter than "somewhere in the string" (an action that only ever shows up inside an example is not being
	/// taught) and it needs no per-language section markers. The action-list region is asserted separately, with the
	/// one marker pair per language: the model reads that list first, so a rule below it is not enough.</para>
	/// </summary>
	private static void ContractCoverage()
	{
		string path = FindCompanionsDll();
		if (path == null)
		{
			Skip("contratos x ValidActions", "Companions.dll nao encontrada - rode dotnet build Companions.sln -c Release");
			return;
		}

		const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
		Type chat;
		string[] actions;
		string[] contracts = new string[5];
		string[] languages = { "Pt", "En", "Es", "Fr", "It" };
		try
		{
			Assembly asm = Assembly.LoadFrom(path);
			chat = asm.GetType("Companions.CompanionChatAI", true);
			FieldInfo actionsField = chat.GetField("ValidActions", flags);
			if (actionsField == null)
			{
				Fail("reflexao em CompanionChatAI", "ValidActions nao encontrado");
				return;
			}
			actions = (string[])actionsField.GetValue(null);
			for (int i = 0; i < languages.Length; i++)
			{
				// Contract is a property (its getter reads CompanionLocale, which outside the game answers EN), so the
				// five texts are read from the const fields themselves - the same strings the property picks from.
				FieldInfo field = chat.GetField("Contract" + languages[i], BindingFlags.NonPublic | BindingFlags.Static);
				if (field == null)
				{
					Fail("reflexao em CompanionChatAI", "Contract" + languages[i] + " nao encontrado");
					return;
				}
				contracts[i] = (string)field.GetRawConstantValue();
			}
		}
		catch (Exception e)
		{
			Fail("reflexao em CompanionChatAI (" + path + ")", Describe(e));
			return;
		}

		CheckInt("ValidActions.Length (a fonte do enum do schema)", 37, actions.Length);

		// The heading of the action list and of the rules, per language - the two markers the model's reading order has.
		string[] listStart = { "Ações válidas:", "Valid actions:", "Acciones válidas:", "Actions valides", "Azioni valide" };
		string[] listEnd = { "Regras:", "Rules:", "Reglas:", "Règles", "Regole:" };

		for (int i = 0; i < contracts.Length; i++)
		{
			string language = languages[i];
			string contract = contracts[i] ?? "";
			string prose = StripExamples(contract);

			// (1) every action appears in the prose (a rule or the list - not only inside an example)
			List<string> missingProse = new List<string>();
			for (int a = 0; a < actions.Length; a++)
			{
				if (!HasWord(prose, actions[a]))
				{
					missingProse.Add(actions[a]);
				}
			}
			Check(language + ": as " + actions.Length + " acoes sao ensinadas na prosa do contrato",
				missingProse.Count == 0,
				missingProse.Count == 0 ? null : "nao ensinada(s): " + string.Join(", ", missingProse.ToArray())
					+ " (o modelo nunca tera motivo para responde-las neste idioma)");

			// (2) every action appears in the ACTION LIST region, which is what the model reads first
			int start = contract.IndexOf(listStart[i], StringComparison.Ordinal);
			int end = start < 0 ? -1 : contract.IndexOf(listEnd[i], start + 1, StringComparison.Ordinal);
			if (start < 0 || end < 0)
			{
				Fail(language + ": marcadores da lista de acoes", "\"" + listStart[i] + "\" ate \"" + listEnd[i] + "\"");
				continue;
			}
			string region = contract.Substring(start, end - start);
			List<string> missingList = new List<string>();
			for (int a = 0; a < actions.Length; a++)
			{
				if (!HasWord(region, actions[a]))
				{
					missingList.Add(actions[a]);
				}
			}
			Check(language + ": as " + actions.Length + " acoes estao na lista que o modelo le primeiro",
				missingList.Count == 0,
				missingList.Count == 0 ? null : "fora da lista: " + string.Join(", ", missingList.ToArray()));
		}

		// (3) the schema enum and the contracts are built from the same array: Contract switches on the locale and is
		//     fed to the model, so a contract nobody returns would be dead text. The five are distinct.
		Check("os 5 contratos sao textos distintos", ContractsDistinct(contracts));
	}

	/// <summary>The contract text with every <c>{...}</c> example removed: what is LEFT is the teaching prose.</summary>
	private static string StripExamples(string text)
	{
		StringBuilder output = new StringBuilder(text.Length);
		int depth = 0;
		for (int i = 0; i < text.Length; i++)
		{
			char c = text[i];
			if (c == '{')
			{
				depth++;
				continue;
			}
			if (c == '}')
			{
				if (depth > 0)
				{
					depth--;
					output.Append(' ');
				}
				continue;
			}
			if (depth == 0)
			{
				output.Append(c);
			}
		}
		return output.ToString();
	}

	/// <summary>Whole-word, case-insensitive: "level" is not found inside "levels" nor in "flatten".</summary>
	private static bool HasWord(string text, string word)
	{
		if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(word))
		{
			return false;
		}
		int from = 0;
		while (true)
		{
			int at = text.IndexOf(word, from, StringComparison.OrdinalIgnoreCase);
			if (at < 0)
			{
				return false;
			}
			bool leftOk = at == 0 || !IsWordChar(text[at - 1]);
			int after = at + word.Length;
			bool rightOk = after >= text.Length || !IsWordChar(text[after]);
			if (leftOk && rightOk)
			{
				return true;
			}
			from = at + 1;
		}
	}

	private static bool IsWordChar(char c)
	{
		return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
	}

	private static bool ContractsDistinct(string[] contracts)
	{
		for (int i = 0; i < contracts.Length; i++)
		{
			for (int j = i + 1; j < contracts.Length; j++)
			{
				if (string.Equals(contracts[i], contracts[j], StringComparison.Ordinal))
				{
					Fail("contratos " + i + " e " + j + " sao identicos", "um dos idiomas herdou o texto do outro");
					return false;
				}
			}
		}
		return true;
	}

	/// <summary>
	/// Section (ab): the round trip that broke in 1.9.6, made structural.
	///
	/// <para><c>CompanionChatAI.ValidActions</c> is the single source of the JSON schema's "enum", so it is the exact
	/// list of actions the model is allowed to answer. <c>LlmIntent.Canonicalize</c>, however, only returns actions
	/// its own <c>Aliases</c> table knows - anything else falls through to <c>"none"</c> at the end. Every action was
	/// added to the table as it was added to the vocabulary, except <c>level</c>: the model answered
	/// <c>{"action":"level"}</c> and the parser rewrote it to <c>"none"</c>, the <c>LevelIntent</c> net rescued the
	/// message afterwards and the log said "o modelo devolveu none" (it had not). The order worked, so nothing
	/// looked wrong.</para>
	///
	/// <para>This section is the invariant that closes that gap: for each of the actions, <c>Parse</c> of the very
	/// JSON the schema allows must come back with the same action. A new action that forgets the alias table now
	/// fails here, by name, instead of depending on a net existing for it.</para>
	/// </summary>
	private static void CanonicalRoundTrip()
	{
		string path = FindCompanionsDll();
		if (path == null)
		{
			Skip("round trip das acoes", "Companions.dll nao encontrada - rode dotnet build Companions.sln -c Release");
			return;
		}

		string[] actions;
		try
		{
			Assembly asm = Assembly.LoadFrom(path);
			FieldInfo field = asm.GetType("Companions.CompanionChatAI", true)
				.GetField("ValidActions", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
			if (field == null)
			{
				Fail("reflexao em CompanionChatAI", "ValidActions nao encontrado");
				return;
			}
			actions = (string[])field.GetValue(null);
		}
		catch (Exception e)
		{
			Fail("reflexao em CompanionChatAI (" + path + ")", Describe(e));
			return;
		}

		// A handful of actions are answered by the model together with a field that changes the canonical action on
		// purpose (an "attack" with no creature named is the hunt JOB - see Canonicalize). Those are the documented
		// cases and are asserted with their real expected value below; everything else is a plain identity.
		List<string> aliased = new List<string>();
		for (int i = 0; i < actions.Length; i++)
		{
			string action = actions[i];
			LlmIntent intent = LlmIntent.Parse("{\"action\":\"" + action + "\"}");
			string expected = action == "attack" ? "hunt" : action;
			if (expected != action)
			{
				aliased.Add(action + "->" + expected);
			}
			Check("Parse({\"action\":\"" + action + "\"}) volta como \"" + expected + "\"",
				string.Equals(expected, intent.Action, StringComparison.Ordinal),
				"obtido \"" + intent.Action + "\" - a acao esta no schema mas nao na tabela Aliases do LlmIntent");
		}
		if (aliased.Count > 0)
		{
			Out.WriteLine("nota: " + aliased.Count + " acao(oes) com desvio documentado: " + string.Join(", ", aliased.ToArray()));
		}
	}

	/// <summary>
	/// Section (ac): <c>BuildBlueprintSkip</c> - <b>why</b> a <c>.blueprint</c> line does not become part of the plan.
	///
	/// <para>The class is four lines of decision, so the <b>reason table</b> is what is really being pinned: the two
	/// strings are not messages shown for a second, they are the enum the rest of the mod compares against.
	/// <c>BuildBlueprintReport</c> fills <c>SkippedReasons</c> with them, <c>MHR_Build</c> logs them and the DevBridge
	/// publishes them as <c>skippedReasons</c> in <c>blueprints.inspect</c>. Renaming one breaks the panel and the
	/// bridge with no compiler error anywhere - which is exactly what this section is for.</para>
	/// </summary>
	private static void BlueprintSkips()
	{
		// the contract: these two spellings are consumed elsewhere, so they are asserted, not just used
		CheckText("NotAPiece e o texto do contrato", "not a piece here", BuildBlueprintSkip.NotAPiece);
		CheckText("RecipeLocked e o texto do contrato", "recipe not unlocked", BuildBlueprintSkip.RecipeLocked);

		// the decision table, all four combinations of the two questions the caller answers
		SkipCase(false, false, BuildBlueprintSkip.NotAPiece, "sem Piece: a receita nem e perguntada");
		SkipCase(false, true, BuildBlueprintSkip.NotAPiece, "sem Piece mesmo com a receita conhecida");
		SkipCase(true, false, BuildBlueprintSkip.RecipeLocked, "e peca, mas o jogador nao desbloqueou");
		SkipCase(true, true, null, "e peca e a receita e conhecida: entra no plano");

		// the two reasons must be distinguishable: if they ever collapse, the panel can no longer tell the player
		// whether to unlock the recipe or give up the prefab
		Check("as duas razoes sao textos diferentes",
			!string.Equals(BuildBlueprintSkip.NotAPiece, BuildBlueprintSkip.RecipeLocked, StringComparison.Ordinal));
	}

	private static void SkipCase(bool hasPiece, bool recipeKnown, string expected, string why)
	{
		CheckText("Reason(hasPiece: " + (hasPiece ? "true" : "false") + ", recipeKnown: "
			+ (recipeKnown ? "true" : "false") + ") - " + why,
			expected, BuildBlueprintSkip.Reason(hasPiece, recipeKnown));
	}

	/// <summary>
	/// Section (ad): <c>LlmProviders</c> - the provider table behind the panel's dropdown.
	///
	/// <para>Nothing here is about the <b>URLs</b> being right (they are the ones the panel shows and they work); it is
	/// about <c>Suggest</c> mapping a URL back to its item. That comparison is <b>tolerant on purpose</b>: the player
	/// pastes the URL with a trailing slash, without the <c>/v1</c>, or with <c>/chat/completions</c> (which
	/// <c>LlmClient</c> appends by itself), and <c>localhost</c> is the same LM Studio as <c>127.0.0.1</c>. Four
	/// normalisations run in a fixed order - the trailing slash before <c>/chat/completions</c>, or
	/// <c>…/v1/chat/completions/</c> would not match - and nothing outside the game would notice if one moved.</para>
	/// </summary>
	private static void ProviderTable()
	{
		CheckInt("Count: 3 provedores + Personalizado", 4, LlmProviders.Count);
		CheckInt("CustomIndex e o ultimo", 3, LlmProviders.CustomIndex);
		CheckText("IdAt(0)", "Groq", LlmProviders.IdAt(0));
		CheckText("IdAt(1)", "LM Studio", LlmProviders.IdAt(1));
		CheckText("IdAt(2)", "OpenRouter", LlmProviders.IdAt(2));
		Check("Fills(0) (Groq preenche URL e modelo)", LlmProviders.Fills(0));
		Check("Fills(1) (LM Studio preenche URL e modelo)", LlmProviders.Fills(1));
		Check("Fills(2) (OpenRouter preenche URL e modelo)", LlmProviders.Fills(2));
		Check("Fills(3) == false (Personalizado nao preenche nada)", !LlmProviders.Fills(3));
		// the key page drives the "create your key" link; LM Studio and the custom item have none
		Check("KeyUrlAt(0) (Groq tem pagina de chave)", LlmProviders.KeyUrlAt(0).Length > 0);
		Check("KeyUrlAt(1) == \"\" (LM Studio roda local, nao tem chave)", LlmProviders.KeyUrlAt(1).Length == 0);
		Check("KeyUrlAt(2) (OpenRouter tem pagina de chave)", LlmProviders.KeyUrlAt(2).Length > 0);
		Check("KeyUrlAt(3) == \"\" (Personalizado)", LlmProviders.KeyUrlAt(3).Length == 0);

		// out of range never throws: the dropdown may ask for a stale index after the table changes
		CheckText("IdAt(-1) cai no Personalizado", "personalizado", LlmProviders.IdAt(-1));
		CheckText("IdAt(99) cai no Personalizado", "personalizado", LlmProviders.IdAt(99));
		CheckText("BaseUrlAt(99) == \"\" (o item que nao preenche)", "", LlmProviders.BaseUrlAt(99));

		// the tolerant matching - every line here is a URL a player could paste
		SuggestCase("https://api.groq.com/openai/v1", 0, "a URL da tabela");
		SuggestCase("http://127.0.0.1:1234/v1", 1, "o LM Studio, como o painel grava");
		SuggestCase("http://localhost:1234/v1", 1, "o alias localhost do LM Studio");
		SuggestCase("http://127.0.0.1:1234/v1/", 1, "com barra no fim");
		SuggestCase("http://127.0.0.1:1234/v1/chat/completions", 1, "a URL do endpoint que o cliente monta");
		SuggestCase("http://127.0.0.1:1234/v1/chat/completions/", 1, "barra no fim E o endpoint (a ordem importa)");
		SuggestCase("https://api.groq.com/openai", 0, "sem o /v1");
		SuggestCase("  HTTPS://API.GROQ.COM/OPENAI/V1  ", 0, "espacos e caixa alta");
		SuggestCase("https://openrouter.ai/api/v1", 2, "a URL da tabela");
		SuggestCase("http://meu-servidor:8080/v1", 3, "Ollama/servidor proprio cai no Personalizado");
		SuggestCase("", 3, "config sem URL");
		SuggestCase(null, 3, "config sem valor");
	}

	private static void SuggestCase(string url, int expected, string why)
	{
		CheckInt("Suggest(" + Show(url) + ") - " + why, expected, LlmProviders.Suggest(url));
	}

	/// <summary>
	/// Section (af): <c>LevelResume</c> - <b>which ending of the leveling hands the site back to the build</b>.
	///
	/// <para>This is the pendência 12 of the building backlog, and it is the reason the section exists: the resume set
	/// was three keys written inline in <c>MHR_Level.Finish</c>, and <c>hc_ai_build_too_steep</c> was not one of them.
	/// With <c>[Building] LevelGround</c> on by default - the <b>product</b> recipe, the run nobody has played yet - a
	/// steep site ended the whole order instead of building what fits. Nothing failed; the order simply stopped, and it
	/// only shows up in a game run. The table is now pure and asserted here.</para>
	///
	/// <para>The case that must NOT resume is <c>hc_ai_build_no_hoe</c>: without a hoe the ground is never touched, so
	/// the build that came back would measure the same variation, ask for the same leveling and end the same way - an
	/// order that restarts itself forever. <c>null</c> (a silent end) has no reason to bring the build back either.</para>
	/// </summary>
	private static void LevelResumes()
	{
		ResumeCase("hc_ai_level_done", true, "o terreno foi nivelado - a obra volta");
		ResumeCase("hc_ai_level_already", true, "o terreno ja estava plano - a obra volta");
		ResumeCase("hc_ai_level_failed", true, "o nivelamento desistiu no piso do jogo - a obra volta com o que tem");
		ResumeCase("hc_ai_build_too_steep", true, "pendencia 12: o terreno e ingreme demais - a obra volta mesmo assim");
		ResumeCase("hc_ai_build_no_hoe", false, "sem enxada o terreno nunca foi tocado - voltar seria repetir a mesma recusa");
		ResumeCase(null, false, "fim silencioso nao devolve nada");
		ResumeCase("", false, "chave vazia nao devolve nada");
		ResumeCase("hc_ai_level_whatever", false, "chave desconhecida nao devolve nada (a tabela e fechada)");

		// the two "won" endings share one line; the two "not ready" endings say different things on purpose - the player
		// reads the log to know whether the ground is ready or the house is going up on a slope
		CheckText("Message(done)", "the ground is ready - back to the build", LevelResume.Message("hc_ai_level_done"));
		CheckText("Message(already)", "the ground is ready - back to the build", LevelResume.Message("hc_ai_level_already"));
		CheckText("Message(failed)", "the ground is as flat as it gets - back to the build",
			LevelResume.Message("hc_ai_level_failed"));
		Check("Message(too_steep) fala do declive (nao diz que o chao esta pronto)",
			LevelResume.Message("hc_ai_build_too_steep") != null
			&& LevelResume.Message("hc_ai_build_too_steep").IndexOf("steep", StringComparison.OrdinalIgnoreCase) >= 0,
			"obtido: " + Show(LevelResume.Message("hc_ai_build_too_steep")));
		Check("Message(no_hoe) == null (nao retoma, nao fala de retomada)", LevelResume.Message("hc_ai_build_no_hoe") == null,
			"obtido: " + Show(LevelResume.Message("hc_ai_build_no_hoe")));
		// the invariant that makes the pair safe: there is a line exactly when the build resumes
		Check("Resumes(x) verdadeiro <=> Message(x) nao nulo (os dois andam juntos)",
			LevelResume.Resumes("hc_ai_build_too_steep") && LevelResume.Message("hc_ai_build_too_steep") != null
			&& !LevelResume.Resumes("hc_ai_build_no_hoe") && LevelResume.Message("hc_ai_build_no_hoe") == null);
	}

	private static void ResumeCase(string key, bool expected, string why)
	{
		bool actual = LevelResume.Resumes(key);
		Check("Resumes(" + (key == null ? "null" : "\"" + key + "\"") + ") -> " + (expected ? "true" : "false") + " ("
			+ why + ")", actual == expected, "obtido " + (actual ? "true" : "false"));
	}

	/// <summary>
	/// Section (ag): <c>BuildPlanOrder</c> - <b>a reorder of the plan must not change which piece an index names</b>.
	///
	/// <para>Every structure of the build state that remembers a piece does it by its <b>position in the plan</b>:
	/// <c>PlacedIndices</c>, <c>SkippedIndices</c>, <c>PulledSupport</c>, <c>FallAdjusted</c>, <c>FallbackPosts</c>,
	/// <c>BarrierServed</c>, <c>Index</c>, <c>PullUntil</c>, <c>ResumeIndex</c>, <c>WalkFor</c>, and on a site the
	/// group's <c>Done</c>/<c>SkippedIndex</c>/<c>Claims</c>/<c>BodyAt</c>/<c>RetryEntry.Index</c>. Four functions of
	/// <c>MHR_Build</c> used to change that order without moving any of them (<c>PreferNearFloor</c> swapped two steps,
	/// <c>InsertBenchStep</c> inserted one, <c>PullFloorsAbove</c> rebuilt the list, <c>PullSupports</c> removed and
	/// re-inserted), so a piece already up could be paid again and a skipped index could name a good piece. Nothing
	/// failed - the order just lost and duplicated pieces, which only a game run shows.</para>
	///
	/// <para>What the section pins is the <b>map</b> (<c>Map</c>/<c>Remap</c>/<c>RemapAll</c>) and, above everything, the
	/// invariant the whole thing exists for: after a swap, an insert, a removal and an append, <b>the index marked as
	/// done points at the same prefab</b>. The three kinds of change are the callers' own - a swap of two positions
	/// (<c>PreferNearFloor</c>), a step inserted before another (<c>InsertBenchStep</c>) and a pull that moves a handful
	/// to the front (<c>PullFloorsAbove</c>/<c>PullSupports</c>).</para>
	/// </summary>
	private static void PlanOrders()
	{
		// --- Map: the old index of each new position, read backwards
		PlanMap("identidade", new[] { 0, 1, 2, 3 }, 4, new[] { 0, 1, 2, 3 });
		PlanMap("troca de dois (PreferNearFloor)", new[] { 3, 1, 2, 0 }, 4, new[] { 3, 1, 2, 0 });
		PlanMap("insercao no meio (InsertBenchStep): -1 e a nova peca", new[] { 0, 1, -1, 2, 3 }, 4, new[] { 0, 1, 3, 4 });
		PlanMap("puxada para a frente (PullSupports): 2,3 na frente do 1",
			new[] { 0, 2, 3, 1, 4 }, 5, new[] { 0, 3, 1, 2, 4 });
		PlanMap("append: o passo novo nao move ninguem", new[] { 0, 1, 2, -1 }, 3, new[] { 0, 1, 2 });
		PlanMap("remocao: o indice que saiu vira -1", new[] { 0, 2, 3 }, 4, new[] { 0, -1, 1, 2 });
		PlanMap("ordem nula: nada do plano antigo sobrevive", null, 3, new[] { -1, -1, -1 });
		PlanMap("plano vazio: o mapa nao tem entrada", new int[0], 0, new int[0]);
		PlanMap("indice fora do plano antigo e ignorado", new[] { 0, 7, -3, 1 }, 2, new[] { 0, 3 });
		// a repeated index keeps the FIRST position: the plan holds a step once, and a second entry is a caller bug
		PlanMap("indice repetido: vale a primeira posicao", new[] { 2, 0, 2 }, 3, new[] { 1, -1, 0 });

		// --- Remap: the single index, and the -1 for an index that is not in the old plan at all
		CheckInt("Remap(identidade, 2)", 2, BuildPlanOrder.Remap(new[] { 0, 1, 2 }, 2));
		CheckInt("Remap(insercao, 2): o 2 andou para 3", 3, BuildPlanOrder.Remap(new[] { 0, 1, 3, 4 }, 2));
		CheckInt("Remap(indice que saiu)", -1, BuildPlanOrder.Remap(new[] { 0, -1, 1 }, 1));
		CheckInt("Remap(-1): nao ha peca", -1, BuildPlanOrder.Remap(new[] { 0, 1 }, -1));
		CheckInt("Remap(passou do plano)", -1, BuildPlanOrder.Remap(new[] { 0, 1 }, 2));
		CheckInt("Remap(mapa nulo)", -1, BuildPlanOrder.Remap(null, 0));

		// --- RemapAll: a set of indices moves whole, and an index with no place loses the entry (never a -1 in a set)
		PlanSet("conjunto vazio", new[] { 0, 1, 2 }, new int[0], new int[0]);
		PlanSet("conjunto que fica igual", new[] { 0, 1, 2 }, new[] { 1, 2 }, new[] { 1, 2 });
		PlanSet("conjunto que anda com a insercao", new[] { 0, -1, 1, 2, 3 }, new[] { 1, 2 }, new[] { 2, 3 });
		PlanSet("conjunto que perde o indice removido", new[] { 0, 2 }, new[] { 0, 1, 2 }, new[] { 0, 1 });
		PlanSet("conjunto nulo", new[] { 0, 1 }, null, new int[0]);

		// --- THE invariant: the index marked as done still points at the same prefab
		PlanIdentity("troca de dois", new[] { 0, 1, 2, 3 }, 2, new[] { 3, 1, 2, 0 });
		PlanIdentity("insercao no meio", new[] { 0, 1, 2, 3 }, 1, new[] { 0, -1, 1, 2, 3 });
		PlanIdentity("puxada para a frente", new[] { 0, 1, 2, 3, 4 }, 4, new[] { 0, 2, 3, 1, 4 });
		PlanIdentity("append", new[] { 0, 1, 2 }, 2, new[] { 0, 1, 2, -1 });
	}

	/// <summary>Section (ag): one <c>Map</c> case - the new plan as old indices, the old size, and the expected map.</summary>
	private static void PlanMap(string name, int[] order, int oldCount, int[] expected)
	{
		int[] map = BuildPlanOrder.Map(order, oldCount);
		CheckInts("Map(" + name + ")", expected, map);
	}

	/// <summary>
	/// Section (ah): <c>ClimbRule</c> - <b>which step up is a way out of an enclosure</b>.
	///
	/// <para>This is the defect of 12/11/2026 and the reason the section exists: <c>MHR_Mover.FindClimb</c> broke on
	/// every built piece taller than one meter, so a companion surrounded by the fence, the beams and the half walls he
	/// had just placed saw no way up in any of the 16 directions and stood still for over 20 minutes. Nothing threw;
	/// the companion simply had nowhere to go, and it only shows up in a game run.</para>
	///
	/// <para>What the table pins, in the order that matters: land is walked at any slope up to the cliff limit
	/// (<c>TerrainStepMax</c>); a <b>built</b> step is vaulted only up to <c>BuiltStepMax</c> <b>and</b> has somewhere to
	/// stand; and a step that dips back down ends that direction. The two cases that must NOT become climbable are the
	/// player's own house wall (2 m, no lip) and the low piece with a void behind it - climbing either would leave the
	/// companion worse off than standing still.</para>
	/// </summary>
	private static void ClimbRules()
	{
		// --- land: walked, not vaulted - the slope limit is the cliff limit
		Check("terreno 0,3 m sobe (degrau de encosta)", ClimbRule.CanClimb(0.3f, true, false));
		Check("terreno 1,2 m sobe (mais alto que um pulo, e chao)", ClimbRule.CanClimb(1.2f, true, false));
		Check("terreno 5,9 m sobe (ainda abaixo do limite de encosta)", ClimbRule.CanClimb(5.9f, true, false));
		Check("terreno 6,1 m NAO sobe (e um penhasco, nao uma encosta)", !ClimbRule.CanClimb(6.1f, true, false));

		// --- built: vaulted up to the ceiling, and only with somewhere to stand past the lip
		Check("obra 0,9 m com apoio sobe (a cerca que o cercou)", ClimbRule.CanClimb(0.9f, false, true));
		Check("obra 1,5 m com apoio sobe (o vao do deck)", ClimbRule.CanClimb(1.5f, false, true));
		Check("obra 1,0 m SEM apoio NAO sobe (beirada de telhado, parede alta)",
			!ClimbRule.CanClimb(1.0f, false, false));
		Check("obra 1,7 m com apoio NAO sobe (acima do teto de pulo)", !ClimbRule.CanClimb(1.7f, false, true));
		Check("obra 2,0 m com apoio NAO sobe (a parede da casa do jogador)",
			!ClimbRule.CanClimb(2.0f, false, true));
		Check("obra 3,0 m com apoio NAO sobe", !ClimbRule.CanClimb(3.0f, false, true));
		Check("descer 0,5 m com apoio sobe (o degrau negativo e chao)",
			ClimbRule.CanClimb(-0.5f, false, true));

		// --- the boundary itself, so a later edit of the constant has to face this line
		Check("BuiltStepMax == 1,6 (o teto do pulo de obra)", ClimbRule.BuiltStepMax == 1.6f);
		Check("exatamente BuiltStepMax com apoio sobe", ClimbRule.CanClimb(ClimbRule.BuiltStepMax, false, true));
		Check("BuiltStepMax + 0,01 com apoio NAO sobe",
			!ClimbRule.CanClimb(ClimbRule.BuiltStepMax + 0.01f, false, true));
		Check("TerrainStepMax == 6 (o mesmo limite que o FindClimb ja tinha)", ClimbRule.TerrainStepMax == 6f);

		// --- the second half of the loop: a step that dips back down ends that direction
		Check("KeepsGoing(0) verdadeiro", ClimbRule.KeepsGoing(0f));
		Check("KeepsGoing(-1,4) verdadeiro (desce um pouco, ainda e caminho)",
			ClimbRule.KeepsGoing(-1.4f));
		Check("KeepsGoing(-1,6) falso (mergulha: nao e caminho de subida)",
			!ClimbRule.KeepsGoing(-1.6f));
	}

	/// <summary>Section (ag): one <c>RemapAll</c> case - the map built from a new plan, the set, and where the set goes.</summary>
	private static void PlanSet(string name, int[] order, int[] set, int[] expected)
	{
		List<int> old = set == null ? null : new List<int>(set);
		HashSet<int> moved = BuildPlanOrder.RemapAll(BuildPlanOrder.Map(order, CountOld(order, set)), old);
		int[] actual = new int[moved.Count];
		moved.CopyTo(actual);
		Array.Sort(actual);
		int[] wanted = (int[])expected.Clone();
		Array.Sort(wanted);
		CheckInts("RemapAll(" + name + ")", wanted, actual);
	}

	/// <summary>How many steps the old plan had, for a <c>RemapAll</c> case: the highest index the order or the set names,
	/// plus one (the map is built for the old plan and an index past it is an index of a piece that never existed).</summary>
	private static int CountOld(int[] order, int[] set)
	{
		int top = -1;
		if (order != null)
		{
			for (int i = 0; i < order.Length; i++)
			{
				top = Math.Max(top, order[i]);
			}
		}
		if (set != null)
		{
			for (int i = 0; i < set.Length; i++)
			{
				top = Math.Max(top, set[i]);
			}
		}
		return top + 1;
	}

	/// <summary>
	/// Section (ag): THE invariant, in one place - <paramref name="oldPlan"/> is the prefab of each step of the old plan
	/// (numbered by the case, which is why a read "piece #N" is the array's value), <paramref name="doneAt"/> is the step
	/// the mod counted as done, and <paramref name="order"/> is the reorder. After it, the index the map answers for the
	/// done step must hold a step whose <b>prefab says the same number</b>. A failure here is exactly the defect: the
	/// number the mod counted as done naming a different piece.
	///
	/// <para><c>-1</c> in <paramref name="order"/> is a step that did not exist before and it becomes <c>fresh</c> in the
	/// rebuilt plan (the workbench <c>InsertBenchStep</c> adds).</para>
	/// </summary>
	private static void PlanIdentity(string name, int[] oldPlan, int doneAt, int[] order)
	{
		int[] map = BuildPlanOrder.Map(order, oldPlan.Length);
		int at = BuildPlanOrder.Remap(map, doneAt);
		// the plan as Reorder builds it: the prefab of each position of the new plan
		int[] rebuilt = new int[order.Length];
		for (int i = 0; i < order.Length; i++)
		{
			rebuilt[i] = order[i] >= 0 ? oldPlan[order[i]] : PlanFresh;
		}
		bool ok = at >= 0 && at < rebuilt.Length && rebuilt[at] == oldPlan[doneAt];
		Check("invariante (" + name + "): a peca feita #" + oldPlan[doneAt] + " continua na posicao " + at
			+ " depois do reorder", ok,
			"obtido: posicao " + at + " tem " + (at >= 0 && at < rebuilt.Length ? "#" + rebuilt[at] : "(nada)"));
	}

	/// <summary>The prefab of a step an <c>order</c> entry of -1 creates in a section (ag) case: it must not be one of the
	/// plan's own numbers, so a moved done index can never "coincidentally" land on it.</summary>
	private const int PlanFresh = -99;

	/// <summary>An int array vs. an int array, printed whole on a failure (a map is short and the positions are the case).</summary>
	private static void CheckInts(string name, int[] expected, int[] actual)
	{
		bool ok = expected.Length == actual.Length;
		for (int i = 0; ok && i < expected.Length; i++)
		{
			ok = expected[i] == actual[i];
		}
		Check(name + " -> [" + string.Join(", ", Array.ConvertAll(expected, v => v.ToString())) + "]", ok,
			"obtido [" + string.Join(", ", Array.ConvertAll(actual, v => v.ToString())) + "]");
	}

	/// <summary>
	/// Section (ae): the <b>log the linked rules produce</b>, read as an instrument.
	///
	/// <para>Every <c>CompanionsPlugin.Log</c> call in the linked files is inside a <c>catch</c> (measured: 7 calls in
	/// the six nets and the item map). So a debug line during a green run is not noise - it means some rule threw and
	/// the exception was swallowed, which is a bug no expected value can catch: the nets answer <c>null</c> on the way
	/// out, and a case that expects <c>null</c> ("an order naming an item nobody knows", "a message that is not an
	/// order") passes for the wrong reason. Before this section the counters existed and nobody read them.</para>
	///
	/// <para>The counter is <b>reset once, in Main</b>, before the first section, so what is read here covers the whole
	/// run. The instrument is proven first (a counter nobody increments would make the claim vacuous) without zeroing
	/// the evidence: the probe compares the value before and after one call, not against a hardcoded number.</para>
	/// </summary>
	private static void QuietLog()
	{
		// what the whole run produced, read BEFORE the probe so the probe cannot contaminate the evidence
		int runLines = CompanionsPlugin.Log.DebugLines;

		// the instrument: the counters move on a call, proved by delta (not against a hardcoded number)
		CompanionsPlugin.Log.LogDebug("(ae) prova do contador");
		CheckInt("o contador conta um LogDebug (delta)", runLines + 1, CompanionsPlugin.Log.DebugLines);

		// and now the claim of the whole run. The detail names the message, because the first thing anybody asks is
		// WHICH rule threw.
		Check("nenhuma regra engoliu excecao nas secoes (a)-(af): " + runLines + " linha(s) de debug",
			runLines == 0,
			"ultima linha: " + (CompanionsPlugin.Log.Last.Count == 0
				? "(nenhuma)"
				: CompanionsPlugin.Log.Last[CompanionsPlugin.Log.Last.Count - 1]));
	}

	private static void CrossCheckDoorRule()
	{
		string path = FindCompanionsDll();
		if (path == null)
		{
			Skip("cross-check da DLL", "Companions.dll nao encontrada - rode dotnet build Companions.sln -c Release");
			return;
		}
		Out.WriteLine("dll: " + path);
		CheckDllFreshness(path);

		const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
		Assembly asm;
		MethodInfo door;
		MethodInfo job;
		try
		{
			asm = Assembly.LoadFrom(path);
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

		// 11/11/2026: the list above is a hand-kept corpus, and the schema has more actions than it names - a new action
		// ("build", "level", the next one) reaches the real JobMode and is never compared with the copy. This walks the
		// DLL's own ValidActions instead (the single source of the JSON schema's enum), so the corpus cannot fall behind.
		CrossCheckJobModeOverSchema(asm, job);
	}

	/// <summary>
	/// Section (h), part 2 (11/11/2026): the copy of <c>JobMode</c> against <b>every action the schema allows</b>.
	///
	/// <para>The hand-kept corpus was 18 entries; <c>CompanionChatAI.ValidActions</c> has 37. Reading the list from the
	/// DLL removes the chance of the corpus ageing: adding an action to the schema and a job to the real method while
	/// forgetting the copy now fails here, by name.</para>
	/// </summary>
	private static void CrossCheckJobModeOverSchema(Assembly asm, MethodInfo job)
	{
		string[] schema;
		try
		{
			FieldInfo field = asm.GetType("Companions.CompanionChatAI", true)
				.GetField("ValidActions", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
			if (field == null)
			{
				Fail("reflexao em CompanionChatAI", "ValidActions nao encontrado");
				return;
			}
			schema = (string[])field.GetValue(null);
		}
		catch (Exception e)
		{
			Fail("reflexao em CompanionChatAI.ValidActions", Describe(e));
			return;
		}
		if (schema == null || schema.Length == 0)
		{
			Fail("CompanionChatAI.ValidActions", "a lista veio vazia - o schema nao tem acao nenhuma");
			return;
		}
		for (int i = 0; i < schema.Length; i++)
		{
			string action = schema[i];
			int local = CompanionOrders.JobMode(action);
			int real;
			try
			{
				real = (int)job.Invoke(null, new object[] { action });
			}
			catch (TargetInvocationException e)
			{
				Fail("DLL JobMode (schema) " + Show(action), Describe(e.InnerException ?? e));
				continue;
			}
			Check("JobMode(" + Show(action) + ") [ValidActions]: copia == DLL", local == real,
				"copia " + local + ", dll " + real);
		}
		Out.WriteLine("nota: " + schema.Length + " acoes do ValidActions comparadas (o corpus fixo tem 18)");
	}

	/// <summary>
	/// Section (h), part 3 (11/11/2026): <b>a DLL é a do código?</b>
	///
	/// <para>The cross-check loads whatever <c>Companions.dll</c> it finds. If <c>src/</c> changed and nobody built, it
	/// compares the copies against an <b>older</b> compilation and reports green - the one section whose job is to catch
	/// drift would be blind to its own staleness. This compares the DLL's timestamp with the newest <c>.cs</c> under
	/// <c>src/Companions</c> (<c>obj/</c> and <c>bin/</c> excluded).</para>
	///
	/// <para>Why a note and not a FAIL: <c>git checkout</c> rewrites the mtime of files that did not change, so a fresh
	/// branch would scream for nothing. <c>--strict</c> (Program.Main) turns it into a FAIL for a release gate.</para>
	/// </summary>
	private static void CheckDllFreshness(string dllPath)
	{
		DateTime dll;
		try
		{
			dll = File.GetLastWriteTimeUtc(dllPath);
		}
		catch (Exception e)
		{
			Fail("mtime da DLL", Describe(e));
			return;
		}

		// the newest source, walking up from the DLL's own repo (bin/Release/net472 -> the repo root)
		string newestPath = null;
		DateTime newest = DateTime.MinValue;
		for (int depth = 0; depth < 8; depth++)
		{
			DirectoryInfo dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
			for (int up = 0; up < depth && dir != null; up++)
			{
				dir = dir.Parent;
			}
			if (dir == null)
			{
				break;
			}
			string sourceDir = Path.Combine(dir.FullName, @"src\Companions");
			if (!Directory.Exists(sourceDir))
			{
				continue;
			}
			ScanSources(sourceDir, ref newest, ref newestPath);
			break;
		}
		if (newestPath == null)
		{
			Skip("a DLL e a do codigo", "src/Companions nao encontrada a partir de " + AppDomain.CurrentDomain.BaseDirectory);
			return;
		}

		string dllText = dll.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
		string srcText = newest.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
		if (dll >= newest)
		{
			Check("a DLL e a do codigo (a mais nova)", true);
			Out.WriteLine("nota: dll " + dllText + " >= fonte " + srcText + " (" + Path.GetFileName(newestPath) + ")");
			return;
		}

		string detail = "dll " + dllText + " e MAIS VELHA que " + Path.GetFileName(newestPath) + " (" + srcText
			+ ") - o cross-check esta comparando com uma compilacao antiga; rode dotnet build Companions.sln -c Release"
			+ (_strict ? "" : " (vira FAIL com --strict)");
		if (_strict)
		{
			Fail("a DLL e a do codigo", detail);
		}
		else
		{
			Out.WriteLine("nota: " + detail);
			Check("a DLL e a do codigo (a mais nova)", true, detail);
		}
	}

	/// <summary>The newest <c>.cs</c> of a folder, skipping <c>obj/</c> and <c>bin/</c> (the build's own output).</summary>
	private static void ScanSources(string folder, ref DateTime newest, ref string newestPath)
	{
		string[] files;
		try
		{
			files = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories);
		}
		catch (Exception)
		{
			return;
		}
		for (int i = 0; i < files.Length; i++)
		{
			string file = files[i];
			if (file.IndexOf(@"\obj\", StringComparison.OrdinalIgnoreCase) >= 0
				|| file.IndexOf(@"\bin\", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				continue;
			}
			DateTime stamp = File.GetLastWriteTimeUtc(file);
			if (stamp > newest)
			{
				newest = stamp;
				newestPath = file;
			}
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

	/// <summary>
	/// Runs <paramref name="body"/> with the four gather lists set to the given text, then puts them back exactly as
	/// they were - including the ones the caller passed as null. That is the point of the helper (11/11/2026): a test
	/// must never read the factory default that <c>Stubs.cs</c> happens to carry, because then it proves the copy
	/// and not the rule. The stub's <c>ConfigFile</c> is never written to disk, so this only changes this process.
	///     <para>The values are restored in a <c>finally</c>: a FAIL inside a case must not leak a list into the next
	/// section (that is how a "green for the wrong reason" run is born).</para>
	/// </summary>
	private static void WithLists(string wood, string stone, string ore, string forage, Action body)
	{
		string oldWood = ModConfig.GatherWood.Value;
		string oldStone = ModConfig.GatherStone.Value;
		string oldOre = ModConfig.GatherOre.Value;
		string oldForage = ModConfig.ForageItems.Value;
		try
		{
			if (wood != null) ModConfig.GatherWood.Value = wood;
			if (stone != null) ModConfig.GatherStone.Value = stone;
			if (ore != null) ModConfig.GatherOre.Value = ore;
			if (forage != null) ModConfig.ForageItems.Value = forage;
			body();
		}
		finally
		{
			ModConfig.GatherWood.Value = oldWood;
			ModConfig.GatherStone.Value = oldStone;
			ModConfig.GatherOre.Value = oldOre;
			ModConfig.ForageItems.Value = oldForage;
		}
	}

	// ============================================================================================ the plumbing

	/// <summary>
	/// Opens a section, runs its cases, and puts the section key back. Every section of <see cref="Main"/> goes through
	/// here (11/11/2026), which is what makes <c>--only</c> and <c>--list</c> possible with a single key per section
	/// instead of a copy of the list in a second place.
	///
	/// <para>The cases of a filtered-out section <b>still run</b>: skipping them would let independent rules drift (a
	/// section reads <c>ModConfig</c>, the log counters and the DLL, and the next one inherits that state), and the
	/// whole run is ~10 s. What the filter changes is only whether <c>Check</c>/<c>Fail</c>/<c>Skip</c> count and print.</para>
	/// </summary>
	private static void RunSection(string key, string title, Action body)
	{
		_current = key;
		Section("(" + key + ") " + title);
		body();
		_current = null;
	}
	private static void Section(string title)
	{
		if (_quiet)
		{
			// --quiet: no headers either - what is left is the FAILs (which Fail always prints) and the summary
			return;
		}
		Out.WriteLine();
		Out.WriteLine("=== " + title + " ===");
	}

	private static void Check(string name, bool ok, string detail = null)
	{
		if (!Counted)
		{
			return;
		}
		if (ok)
		{
			_pass++;
			if (!_quiet)
			{
				Out.WriteLine("PASS  " + name);
			}
			return;
		}
		Fail(name, detail);
	}

	private static void CheckText(string name, string expected, string actual)
	{
		if (!Counted)
		{
			return;
		}
		Check(name + " -> " + Show(expected), string.Equals(expected, actual, StringComparison.Ordinal),
			"obtido " + Show(actual));
	}

	/// <summary>E6: a float assertion with a small tolerance (the template offsets are metres).</summary>
	private static void CheckFloat(string name, float expected, float actual)
	{
		if (!Counted)
		{
			return;
		}
		Check(name + " -> " + expected.ToString("0.##"), Math.Abs(expected - actual) < 0.001f, "obtido " + actual.ToString("0.###"));
	}

	private static void CheckInt(string name, int expected, int actual)
	{
		if (!Counted)
		{
			return;
		}
		Check(name + " -> " + expected, expected == actual, "obtido " + actual);
	}

	/// <summary>
	/// A FAIL is <b>never</b> silent: not in <c>--quiet</c> and not outside <c>--only</c>. The filter narrows what is
	/// <b>counted and printed as passing</b>; a failure anywhere still reaches the summary and the exit code, because a
	/// harness that hides a red case behind a filter is worse than no filter (11/11/2026: with the naive filter,
	/// <c>--only=(p) --strict</c> swallowed the stale-DLL FAIL of section (h) and exited 0).
	/// </summary>
	private static void Fail(string name, string detail)
	{
		_fail++;
		string line = name + (string.IsNullOrEmpty(detail) ? "" : "  [" + detail + "]");
		Failures.Add(line);
		Out.WriteLine("FAIL  " + line);
	}

	/// <summary>A SKIP decides the exit code, so it follows the same rule as <see cref="Fail"/>: never filtered out.</summary>
	private static void Skip(string name, string why)
	{
		_skip++;
		Skips.Add(name + " (" + why + ")");
		Out.WriteLine("SKIP  " + name + "  [" + why + "]");
	}

	/// <summary>
	/// A case whose expected value is the BUG. Not counted as PASS/FAIL: the orchestrator decides.
	///
	/// <para><b>The mark cannot rot</b> (11/11/2026). Until this change <c>KNOWN-FAIL*</c> - the mark that means "the bug
	/// is gone" - was printed and otherwise ignored, so a fixed bug left a line somebody had to remember to remove, and
	/// the run stayed green. It is now a <b>real FAIL</b> with the message "a marca pode sair": keeping a KNOWN-FAIL is
	/// only allowed while the bug is still there. The normal path (<paramref name="stillBroken"/> true) still counts as
	/// neither PASS nor FAIL, exactly as before - it is a record, not an assertion.</para>
	/// </summary>
	private static void KnownFail(string name, bool stillBroken, string detail)
	{
		if (stillBroken)
		{
			// a record, not an assertion: it is counted apart and does not decide the exit code
			if (!Counted)
			{
				return;
			}
			_knownFail++;
			Out.WriteLine("KNOWN-FAIL  " + name + "  [" + detail + "]");
			return;
		}
		Fail(name, detail + " - o bug sumiu: a marca KNOWN-FAIL pode sair");
	}

	/// <summary>The value of a <c>--name=value</c> argument, or null when it is not there.</summary>
	private static string ArgumentValue(string[] args, string prefix)
	{
		for (int i = 0; i < args.Length; i++)
		{
			if (args[i] != null && args[i].StartsWith(prefix, StringComparison.Ordinal))
			{
				return args[i].Substring(prefix.Length);
			}
		}
		return null;
	}

	private static List<string> Sorted(HashSet<string> set)
	{
		List<string> list = new List<string>(set);
		list.Sort(StringComparer.Ordinal);
		return list;
	}

	/// <summary>Is this a section key of <see cref="SectionOrder"/>? (<c>--only</c> refuses the ones that are not.)</summary>
	private static bool IsSectionKey(string key)
	{
		for (int i = 0; i < SectionOrder.Length; i++)
		{
			if (string.Equals(SectionOrder[i].Key, key, StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// <c>--list</c>: prints the sections in the order they run, read from the same <see cref="SectionOrder"/> table
	/// <see cref="Main"/> runs from, so the two cannot disagree. It runs before anything else (no DLL, no game folders
	/// needed), so it also answers "what is in this harness" on a machine without the game installed.
	/// </summary>
	private static void PrintSectionList()
	{
		Out.WriteLine("secoes, na ordem em que rodam (--only=(x) conta so a secao x; as outras rodam e nao contam):");
		for (int i = 0; i < SectionOrder.Length; i++)
		{
			Out.WriteLine("  (" + SectionOrder[i].Key + ") " + SectionOrder[i].Title);
		}
		Out.WriteLine();
		Out.WriteLine("argumentos: --only=(a),(p)  --quiet  --list  --strict");
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
