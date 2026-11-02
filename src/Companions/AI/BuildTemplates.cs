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
		"cabana", "choupana", "chao 2x2", "chao de 2x2", "quatro paredes",
		"hut", "small hut",
		"cabana pequena", "choza",
		"cabane", "cabane simple",
		"casetta", "capanna", "stanza 2x2"
	}, new[]
	{
		new Step("wood_floor", 0f, 0f, 0f, 0f, 0),
		new Step("woodwall", 0f, 1f, 1f, 0f, 1),
		new Step("woodwall", 0f, 1f, -1f, 0f, 1),
		new Step("woodwall", 1f, 1f, 0f, 90f, 1),
		new Step("woodwall", -1f, 1f, 0f, 90f, 1),
		// the roof rests ON the wall: at y = 2 its surface crosses the wall top (y = 2) at z = ±1, the eave hangs 1 m
		// beyond it (z = ±2, y = 1) and the ridge meets at z = 0, y = 3. At y = 3 it would float 1 m above the walls -
		// measured in the player's own hut, 01/11/2026: "UNSUPPORTED: wood_roof_45 (support 0,0 < min 10,0)"
		new Step("wood_roof_45", 0f, 2f, 1f, 0f, 2),
		new Step("wood_roof_45", 0f, 2f, -1f, 180f, 2)
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
		"abrigo", "casa", "casinha", "casa simples", "casa de madeira", "casebre",
		"house", "small house", "shelter", "cabin",
		"casa pequeña", "casita", "refugio",
		"maison", "petite maison", "abri",
		"casa piccola", "casupola", "rifugio"
	}, HouseSteps());

	private static Step[] HouseSteps()
	{
		List<Step> steps = new List<Step>();
		// the house brings its OWN workbench (inside, back-left corner): every wooden piece needs a station in range, so
		// without it the order would need the player to have one - and a bench dropped at the spot ends up INSIDE the
		// house, in the way of the floor (measured 01/11/2026: "a bancada atrapalhou"). It goes FIRST: the floors
		// themselves need the station it provides.
		steps.Add(new Step("piece_workbench", -2.3f, 0.12f, 2f, 0f, 0));
		// the floor: 4x4 tiles of 2x2 m, level 0
		float[] grid = { -3f, -1f, 1f, 3f };
		foreach (float x in grid)
		{
			foreach (float z in grid)
			{
				steps.Add(new Step("wood_floor", x, 0f, z, 0f, 0));
			}
		}
		// the walls, level 1: the FRONT is local -z (the side the player faces when the house goes up)
		//   front: a wall, the DOOR (level 3 - it goes in LAST), the WINDOW (half wall at y=0,5) and a wall
		steps.Add(new Step("woodwall", -3f, 1f, -4f, 0f, 1));
		steps.Add(new Step("wood_wall_half", 1f, 0.5f, -4f, 0f, 1));
		steps.Add(new Step("woodwall", 3f, 1f, -4f, 0f, 1));
		//   back
		foreach (float x in grid)
		{
			steps.Add(new Step("woodwall", x, 1f, 4f, 0f, 1));
		}
		//   left and right
		foreach (float z in grid)
		{
			steps.Add(new Step("woodwall", -4f, 1f, z, 90f, 1));
			steps.Add(new Step("woodwall", 4f, 1f, z, 90f, 1));
		}
		// the roof, level 2: the eave at the wall top (y=2) and the ridge 4 m above it (z=0), 2 pieces per slope
		foreach (float x in grid)
		{
			steps.Add(new Step("wood_roof_45", x, 3f, 3f, 0f, 2));
			steps.Add(new Step("wood_roof_45", x, 5f, 1f, 0f, 2));
			steps.Add(new Step("wood_roof_45", x, 3f, -3f, 180f, 2));
			steps.Add(new Step("wood_roof_45", x, 5f, -1f, 180f, 2));
		}
		// the gables (the triangular ends), level 2: two pieces per side, rising toward the ridge
		foreach (float x in new float[] { -4f, 4f })
		{
			steps.Add(new Step("wood_wall_roof_45", x, 2f, 3f, 90f, 2));
			steps.Add(new Step("wood_wall_roof_45", x, 4f, 1f, 90f, 2));
			steps.Add(new Step("wood_wall_roof_45", x, 2f, -3f, 270f, 2));
			steps.Add(new Step("wood_wall_roof_45", x, 4f, -1f, 270f, 2));
		}
		// the DOOR goes in LAST (level 3): with the four walls up and the door in place the companion is locked INSIDE
		// the house and cannot reach the roof - measured 01/11/2026, "the order took too long" at 33/56. The open door
		// slot lets it walk in and out during the whole build.
		steps.Add(new Step("wood_door", -1f, 1f, -4f, 0f, 3));
		return steps.ToArray();
	}

	private static readonly Template[] All = { House, Hut, Canopy };

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
