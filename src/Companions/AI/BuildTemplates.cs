using System;
using System.Collections.Generic;

namespace Companions;

/// <summary>
/// Fase E6: the <b>templates</b> - a small structure as a list of pieces positioned <b>relative to an anchor</b>, in
/// the order they have to go up (foundation, walls, roof). It is <b>pure</b> (no Unity, no world), so the offline
/// harness can link it and check every alias and every offset - the same design as <see cref="BuildCatalog"/>.
///
/// <para><b>Where the numbers come from</b> (measured in game, 01/11/2026, <c>build.snaps</c> + <c>build.inspect</c>
/// bounds - see <c>docs/re/building-notes.md</c> §11):</para>
/// <list type="bullet">
/// <item><c>wood_floor</c> (2x2): snap points at (±1, 0, ±1) - the origin is the <b>centre</b> of the tile and its
/// top surface is y = 0. A grid of tiles is therefore 2 m apart, centred on the anchor.</item>
/// <item><c>woodwall</c>: snaps at (±1, ±1, 0) - origin at the <b>centre</b> of a 2 m wide, 2 m high wall, so a wall
/// standing on a floor has its origin 1 m above it.</item>
/// <item><c>wood_roof_45</c>: snaps at (±1, ±1, ∓1) - it rises 2 m over 2 m along z, origin at the centre of the
/// span; the eave is 1 m out and the ridge 1 m in, which is what gives the overhang.</item>
/// <item><c>wood_pole2</c>: snaps at (0, ±1, 0) - 2 m tall, origin at the centre.</item>
/// </list>
///
/// <para>A step's <c>Y</c> is measured from the <b>top of the floor</b> (the anchor's y), so level 0 is the floor
/// itself (y = 0), walls stand at y = 1 and roofs at y = 3.</para>
/// </summary>
internal static class BuildTemplates
{
	/// <summary>One piece of a template, relative to the anchor. Offsets in metres, yaw in degrees.</summary>
	internal sealed class Step
	{
		internal readonly string Prefab;
		internal readonly float X;
		internal readonly float Y;
		internal readonly float Z;
		internal readonly float Yaw;
		/// <summary>0 = foundation, 1 = walls, 2 = roof. The plan is always executed in this order.</summary>
		internal readonly int Level;

		internal Step(string prefab, float x, float y, float z, float yaw, int level)
		{
			Prefab = prefab;
			X = x;
			Y = y;
			Z = z;
			Yaw = yaw;
			Level = level;
		}
	}

	internal sealed class Template
	{
		internal readonly string Name;
		/// <summary>How it is called in the log/refusal (the game has no name for a whole structure).</summary>
		internal readonly string Label;
		internal readonly string[] Aliases;
		internal readonly Step[] Steps;

		internal Template(string name, string label, string[] aliases, Step[] steps)
		{
			Name = name;
			Label = label;
			Aliases = aliases;
			Steps = steps;
		}

		/// <summary>The steps in build order (foundation, walls, roof), stable inside a level (the authored order).</summary>
		internal List<Step> Ordered()
		{
			List<Step> list = new List<Step>(Steps);
			list.Sort(delegate (Step a, Step b)
			{
				if (a.Level != b.Level)
				{
					return a.Level.CompareTo(b.Level);
				}
				return Array.IndexOf(Steps, a).CompareTo(Array.IndexOf(Steps, b));
			});
			return list;
		}

		/// <summary>
		/// The horizontal extent of the structure - the ground it needs. E10 uses it to know how much terrain to level
		/// before the first piece.
		/// </summary>
		internal void Footprint(out float minX, out float maxX, out float minZ, out float maxZ)
		{
			minX = float.MaxValue;
			maxX = float.MinValue;
			minZ = float.MaxValue;
			maxZ = float.MinValue;
			foreach (Step step in Steps)
			{
				if (step.X < minX)
				{
					minX = step.X;
				}
				if (step.X > maxX)
				{
					maxX = step.X;
				}
				if (step.Z < minZ)
				{
					minZ = step.Z;
				}
				if (step.Z > maxZ)
				{
					maxZ = step.Z;
				}
			}
		}
	}

	/// <summary>
	/// A 2x2 m hut: one floor tile, four walls and a two-piece gable roof - the acceptance of E6 ("2x2 de piso +
	/// 4 paredes + telhado"). The roof overhangs 1 m on each side and meets at the ridge 2 m above the walls.
	/// </summary>
	private static readonly Template Hut = new Template("hut", "cabana", new[]
	{
		"cabana", "choupana", "barraco", "barracao", "chao 2x2", "chao de 2x2", "quatro paredes",
		"hut", "small hut", "shack", "shed",
		"cabana pequena", "choza",
		"cabane", "cabane simple",
		"casetta", "capanna", "stanza 2x2"
	}, new[]
	{
		new Step("wood_floor", 0f, 0f, 0f, 0f, 0),
		new Step("woodwall", 0f, 1f, 1f, 0f, 1),
		new Step("woodwall", 1f, 1f, 0f, 90f, 1),
		new Step("woodwall", -1f, 1f, 0f, 90f, 1),
		// the roof rests ON the wall: at y = 2 its surface crosses the wall top (y = 2) at z = ±1, the eave hangs 1 m
		// beyond it (z = ±2, y = 1) and the ridge meets at z = 0, y = 3. At y = 3 it would float 1 m above the walls -
		// measured in the player's own hut, 01/11/2026: "UNSUPPORTED: wood_roof_45 (support 0,0 < min 10,0)"
		new Step("wood_roof_45", 0f, 2f, 1f, 0f, 2),
		new Step("wood_roof_45", 0f, 2f, -1f, 180f, 2),
		// E6d (player's request, 02/11/2026): the hut gets a DOOR, in the front wall (local -z) and in LAST - with the
		// four walls up and the door in place the companion would be locked inside during the roof.
		new Step("wood_door", 0f, 1f, -1f, 0f, 3)
	});

	/// <summary>
	/// The bench shelter: a 4x2 m gable roof on four 2 m poles, built <b>around</b> the bench - it is what
	/// <c>CraftingStation.CheckUsable</c> asks for (a roof with 70% cover) and it is the "usável" half of E5.
	/// The poles stand at the eaves, outside the bench's footprint (the bench is 3,7 x 2,1 m).
	/// </summary>
	private static readonly Template Canopy = new Template("canopy", "abrigo da bancada", new[]
	{
		"abrigo da bancada", "abrigo para a bancada", "telhado para a bancada", "telhado da bancada", "cobertura", "toldo",
		"bench shelter", "canopy", "roof for the bench", "shelter for the bench",
		"cobertizo", "cobertizo de la mesa", "techo para la mesa",
		"auvent", "toit pour l etabli",
		"riparo", "riparo del banco", "tettoia", "tetto per il banco"
	}, new[]
	{
		new Step("wood_pole2", 1f, 1f, 2f, 0f, 0),
		new Step("wood_pole2", -1f, 1f, 2f, 0f, 0),
		new Step("wood_pole2", 1f, 1f, -2f, 0f, 0),
		new Step("wood_pole2", -1f, 1f, -2f, 0f, 0),
		new Step("wood_roof_45", 1f, 3f, 1f, 0f, 2),
		new Step("wood_roof_45", -1f, 3f, 1f, 0f, 2),
		new Step("wood_roof_45", 1f, 3f, -1f, 180f, 2),
		new Step("wood_roof_45", -1f, 3f, -1f, 180f, 2)
	});

	/// <summary>
	/// A <b>simple house</b>: 8x8 m (4x4 floor tiles), walls on the four sides, a <b>door</b> and a <b>window</b> on
	/// the front (the side facing the player, local -z), a 45° gable roof with a centred ridge and closed gables.
	///
	/// <para>8x8 is the size that fits the 45° roof cleanly: the piece rises 2 m over 2 m, so a 4 m run per slope is
	/// exactly <b>two pieces</b> and the ridge lands in the middle. A 6 m house would need 1,5 pieces per slope and
	/// put the ridge 1 m off centre.</para>
	///
	/// <para><b>Door and window</b> (measured): <c>wood_door</c> is 2x2 with the same origin as a wall (it takes the
	/// wall's place), and <c>wood_wall_half</c> is 2x1 with its origin at the centre - placed at y = 0,5 it leaves a
	/// 1 m opening above it, which is the window.</para>
	/// </summary>
	private static readonly Template House = new Template("house", "casa", new[]
	{
		"abrigo", "casa", "casinha", "casa simples", "casa de madeira", "casebre", "casa grande",
		"house", "small house", "shelter", "cabin", "big house",
		"casita", "refugio", "casa grande",
		"maison", "petite maison", "abri", "grande maison",
		"casa piccola", "casupola", "rifugio", "casa grande"
	}, HouseSteps(4, 1, 2));

	/// <summary>
	/// The <b>6x6</b> house (player's request, 02/11/2026): the same design as the measured 8x8 with one size down.
	/// The roof is the part that changes: the run from the wall to the ridge is 3 m and a 45 deg piece covers 2 m, so it
	/// takes <b>2 pieces per slope with the eave 1 m past the wall</b> (the player's choice) - and a roof 1 m past the
	/// wall has nothing under it, so the plan brings a <b>2 m post at each corner of the eave</b> (the veranda look).
	/// </summary>
	private static readonly Template House6 = new Template("house6", "casa 6x6", new[]
	{
		"casa 6x6", "casa media", "casa média", "casa mediana", "casa de 6x6", "6x6",
		"house 6x6", "medium house", "6x6 house",
		"casa 6x6 mediana", "casa de 6x6",
		"maison 6x6", "maison moyenne",
		"casa 6x6 media", "casa media", "casa di 6x6"
	}, HouseSteps(3, 2, 0, roofAlongZ: true));

	/// <summary>
	/// The <b>4x4</b> house (player's request, 02/11/2026): two tiles on each side, one 45 deg piece per slope (the
	/// pieces meet exactly at the ridge) and <b>no workbench</b> - a 3,7 x 2,1 m bench inside a 4x4 m room leaves no
	/// floor, so this one needs a bench (any crafting station) within the game's radius. Door and window share the two
	/// front slots.
	/// </summary>
	private static readonly Template House4 = new Template("house4", "casa 4x4", new[]
	{
		"casa 4x4", "casa pequena", "casa pequena 4x4", "casa de 4x4", "4x4", "casebre 4x4",
		"house 4x4", "small house 4x4", "4x4 house",
		"casa pequeña 4x4", "casa pequeña de 4x4", "casita 4x4",
		"maison 4x4", "petite maison 4x4",
		"casa 4x4 piccola", "casetta 4x4"
	}, HouseSteps(2, 0, 1));

	/// <summary>
	/// The <b>tent</b> (player's request, 02/11/2026): a 4x2 m lean-to - two 2 m poles hold the high edge of two 45 deg
	/// pieces at the back and the roof runs down to the ground 2 m in front. Open on every side; 8 wood, and it is
	/// the cheap "cover the bench" shelter (the piece covers 2 m). The poles stand on the roof's own snap points (the
	/// outer corners of the high edge), which is what gives the pieces their support.
	/// </summary>
	private static readonly Template Tent = new Template("tent", "tenda", new[]
	{
		"tenda", "toldo", "abrigo de uma agua", "abrigo de 1 agua", "abrigo de uma água", "barraca",
		"tent", "lean to", "lean-to", "awning",
		"tienda", "toldo de una agua", "toldo de una agua",
		"tente", "auvent incline",
		"tenda da campo", "pensilina", "tettoia a una falda"
	}, new[]
	{
		// the poles at the eave's outer snaps (x = ±2, z = 0): their tops are the roof's high edge
		new Step("wood_pole2", 2f, 1f, 0f, 0f, 0),
		new Step("wood_pole2", -2f, 1f, 0f, 0f, 0),
		// the roof: origin at (y = 1, z = 1), high edge at z = 0 / y = 2 and the eave on the ground at z = 2 / y = 0
		new Step("wood_roof_45", 1f, 1f, 1f, 0f, 2),
		new Step("wood_roof_45", -1f, 1f, 1f, 0f, 2)
	});

	/// <summary>
	/// A house of <paramref name="tiles"/> x <paramref name="tiles"/> tiles of 2 m: 4x4, 6x6 and 8x8 wall to wall. The
	/// 8x8 is the one measured in game (57/57, 01/11/2026) and its poses are unchanged; the smaller ones are the same
	/// recipe with the roof adapted to the run from the wall to the ridge:
	/// <list type="bullet">
	/// <item><b>4x4</b> (half = 2): one 45 deg piece per slope, eave on the wall - the two pieces meet at the ridge.</item>
	/// <item><b>6x6</b> (half = 3): the run is 3 m, so 2 pieces per slope with the eave 1 m past the wall, plus a 2 m
	/// post at each corner of the eave (nothing else would hold it).</item>
	/// <item><b>8x8</b> (half = 4): 2 pieces per slope, eave on the wall.</item>
	/// </list>
	/// </summary>
	private static Step[] HouseSteps(int tiles, int doorSlot, int windowSlot, bool roofAlongZ = false, int wallCourses = 1)
	{
		List<Step> steps = new List<Step>();
		float half = tiles;
		float[] grid = new float[tiles];
		for (int i = 0; i < tiles; i++)
		{
			grid[i] = -half + 1f + 2f * i;
		}
		// the house brings its OWN workbench (inside, back-left corner): every wooden piece needs a station in range.
		// The 4x4 has no room for it (a 3,7 x 2,1 m bench in a 4x4 m room) - that one needs a station nearby.
		// E6d (player's test, 02/11/2026): the 6x6 uses the SAME spot as the 8x8 (back-left, measured poses) - the bench
		// I had moved to the left wall sat exactly over a floor tile and that floor was refused with no_support.
		if (tiles >= 3)
		{
			steps.Add(new Step("piece_workbench", -half + 1.7f, 0.12f, half - 2f, 0f, 0));
		}
		// the floor: tiles x tiles tiles of 2x2 m, level 0
		foreach (float x in grid)
		{
			foreach (float z in grid)
			{
				steps.Add(new Step("wood_floor", x, 0f, z, 0f, 0));
			}
		}
		// the walls: one course is 2 m (the piece's height). A 6x6 needs TWO courses (4 m) - with a single 2 m wall the
		// 45 degree roof (4 m of rise) dominates the facade (player's call, 02/11/2026: "6x6 com paredes de 4 m").
		// The FRONT is local -z: the door slot stays open (the door goes in LAST) and the window slot gets the half wall
		// on the ground course only.
		for (int course = 0; course < wallCourses; course++)
		{
			float y = 1f + 2f * course;
			int level = 1 + course;
			for (int i = 0; i < tiles; i++)
			{
				if (i == doorSlot && course == 0)
				{
					continue;
				}
				steps.Add(i == windowSlot && course == 0
					? new Step("wood_wall_half", grid[i], 0.5f, -half, 0f, level)
					: new Step("woodwall", grid[i], y, -half, 0f, level));
			}
			foreach (float x in grid)
			{
				steps.Add(new Step("woodwall", x, y, half, 0f, level));
			}
			foreach (float z in grid)
			{
				steps.Add(new Step("woodwall", -half, y, z, 90f, level));
				steps.Add(new Step("woodwall", half, y, z, 90f, level));
			}
		}
		// the roof: 45 deg, one piece per 2 m of run from the eave to the ridge, starting at the TOP of the walls. When the
		// run does not fit whole pieces (6x6: 3 m) the eave goes 1 m PAST the wall and a post holds each corner of it (one
		// pole per 2 m of wall). With roofAlongZ the ridge runs along z (the gable faces the door).
		bool veranda = half % 2f > 0.01f;
		float eave = veranda ? half + 1f : half;
		float wallTop = 2f * wallCourses;
		int perSlope = (int)(eave / 2f);
		int roofLevel = wallCourses + 1;
		if (veranda)
		{
			for (int course = 0; course < wallCourses; course++)
			{
				foreach (float a in new float[] { -half, half })
				{
					foreach (float b in new float[] { eave, -eave })
					{
						// the upper pole is a LEVEL 1 piece (like the wall course it stands with): at level 0 the plan check
						// demands ground support and refuses it (its base is 2 m up), and the whole anchor search fails with
						// "no_support" (the player's "nao tem espaco" at a spot where the 8x8 fits, 02/11/2026)
						steps.Add(roofAlongZ
							? new Step("wood_pole2", b, 1f + 2f * course, a, 0f, course)
							: new Step("wood_pole2", a, 1f + 2f * course, b, 0f, course));
					}
				}
			}
		}
		foreach (float along in grid)
		{
			for (int k = 0; k < perSlope; k++)
			{
				float z = eave - 1f - 2f * k;
				float y = wallTop + 1f + 2f * k;
				if (roofAlongZ)
				{
					steps.Add(new Step("wood_roof_45", z, y, along, 90f, roofLevel));
					steps.Add(new Step("wood_roof_45", -z, y, along, 270f, roofLevel));
				}
				else
				{
					steps.Add(new Step("wood_roof_45", along, y, z, 0f, roofLevel));
					steps.Add(new Step("wood_roof_45", along, y, -z, 180f, roofLevel));
				}
			}
		}
		// the gables (the triangular ends), at the top of the walls, following the roof plane. NOTE the yaw: with
		// roofAlongZ the +x side of the gable rises toward -x, which is yaw 180 (yaw 0 rises toward +x) - swapped they go
		// in like wings (player: "as paredes 45 parecem invertidas", 02/11/2026).
		for (int k = 0; k < perSlope; k++)
		{
			float z = eave - 1f - 2f * k;
			float y = wallTop + 2f * k;
			foreach (float end in new float[] { -half, half })
			{
				if (roofAlongZ)
				{
					steps.Add(new Step("wood_wall_roof_45", z, y, end, 180f, roofLevel));
					steps.Add(new Step("wood_wall_roof_45", -z, y, end, 0f, roofLevel));
				}
				else
				{
					steps.Add(new Step("wood_wall_roof_45", end, y, z, 90f, roofLevel));
					steps.Add(new Step("wood_wall_roof_45", end, y, -z, 270f, roofLevel));
				}
			}
		}
		// the DOOR goes in LAST (level after the roof): with the walls up and the door in place the companion is locked
		// INSIDE the house and cannot reach the roof - measured 01/11/2026, "the order took too long" at 33/56.
		steps.Add(new Step("wood_door", grid[doorSlot], 1f, -half, 0f, roofLevel + 1));
		return steps.ToArray();
	}
	private static readonly Template[] All = { House, House6, House4, Tent, Hut, Canopy };

	internal static Template[] Templates()
	{
		return (Template[])All.Clone();
	}

	internal static Template ByName(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return null;
		}
		foreach (Template template in All)
		{
			if (string.Equals(template.Name, name, StringComparison.OrdinalIgnoreCase))
			{
				return template;
			}
		}
		return null;
	}

	/// <summary>The template the text names, or null. Longest alias wins; whole words only (the same rule as the
	/// piece catalog, so "cama" cannot swallow "cabana").</summary>
	internal static Template Resolve(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}
		string haystack = " " + BuildCatalog.Fold(text) + " ";
		Template best = null;
		int bestLength = 0;
		foreach (Template template in All)
		{
			foreach (string alias in template.Aliases)
			{
				string needle = " " + BuildCatalog.Fold(alias) + " ";
				if (needle.Length > bestLength && haystack.IndexOf(needle, StringComparison.Ordinal) >= 0)
				{
					best = template;
					bestLength = needle.Length;
				}
			}
		}
		return best;
	}

	/// <summary>Every prefab a plan may place (the allow-list of the plans - the single-piece order has its own).</summary>
	internal static bool Allowed(string prefab)
	{
		if (string.IsNullOrEmpty(prefab))
		{
			return false;
		}
		foreach (Template template in All)
		{
			foreach (Step step in template.Steps)
			{
				if (string.Equals(step.Prefab, prefab, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
		}
		return false;
	}

	/// <summary>Every prefab used by any template, without duplicates (the harness and the log read it).</summary>
	internal static string[] Prefabs()
	{
		List<string> list = new List<string>();
		foreach (Template template in All)
		{
			foreach (Step step in template.Steps)
			{
				if (!list.Contains(step.Prefab))
				{
					list.Add(step.Prefab);
				}
			}
		}
		return list.ToArray();
	}
}
