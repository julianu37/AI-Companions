using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Companions;

/// <summary>
/// E7: the <b>pure</b> reader of a PlanBuild <c>.blueprint</c> - the format used by BiomeBlueprints, valheimians and
/// the game's own blueprint capture. It is a <b>text</b> file: the headers <c>#Name:</c>, <c>#Creator:</c>,
/// <c>#Description:</c>, <c>#Category:</c>, the sections <c>#SnapPoints</c>, <c>#Terrain</c> and <c>#Pieces</c>, and one
/// line per piece:
/// <code>prefab;category;px;py;pz;rx;ry;rz;rw;info;sx;sy;sz</code>
/// (numbers with a dot; the old files with commas are accepted, and a V1 file has no headers at all - every line is a
/// piece). Unity-free and dependency-free on purpose: the offline harness links this file for the tests (section (k)).
/// </summary>
internal static class BuildBlueprintParser
{
	/// <summary>One piece as plain floats (the mod turns it into a Vector3/Quaternion).</summary>
	internal sealed class Line
	{
		internal string Prefab;
		internal float Px, Py, Pz, Rx, Ry, Rz, Rw, Scale = 1f;
		/// <summary>
		/// 04/11/2026 (the body barrier): the <b>level</b> of the piece - the very key <see cref="Ordered"/> sorts by (its
		/// own height, or the slab a roof/gable covers). Filled by <see cref="Ordered"/>; 0 until then. The build group uses
		/// it to hold back only the pieces ABOVE the storey a body is standing on.
		/// </summary>
		internal float Level;
	}

	/// <summary>
	/// Reads the lines of a blueprint. <c>#Terrain</c>, <c>#SnapPoints</c> and any other <c>#</c> section are skipped
	/// (v1 does not apply terrain); a piece line with fewer than 9 fields is skipped and counted in
	/// <paramref name="broken"/>. Returns false (with <paramref name="error"/>) when no piece could be read.
	/// </summary>
	internal static bool Parse(IEnumerable<string> lines, out string name, out List<Line> pieces, out string error,
		out int broken)
	{
		name = null;
		pieces = new List<Line>();
		error = null;
		broken = 0;
		if (lines == null)
		{
			error = "empty";
			return false;
		}
		bool inPieces = true;      // a V1 file has no headers: every line is a piece
		foreach (string raw in lines)
		{
			if (raw == null)
			{
				continue;
			}
			string line = raw.Trim();
			if (line.Length == 0)
			{
				continue;
			}
			if (line.StartsWith("#Name:", StringComparison.Ordinal))
			{
				name = line.Substring("#Name:".Length).Trim();
				continue;
			}
			if (line.StartsWith("#Pieces", StringComparison.Ordinal))
			{
				inPieces = true;
				continue;
			}
			if (line.StartsWith("#", StringComparison.Ordinal))
			{
				// #Creator/#Description/#Category/#SnapPoints/#Terrain or an unknown one (a mod's extra section):
				// skip its lines until the next known header instead of parsing them as pieces
				inPieces = false;
				continue;
			}
			if (!inPieces)
			{
				continue;
			}
			Line piece = ParseLine(line);
			if (piece == null)
			{
				broken++;
				continue;
			}
			pieces.Add(piece);
		}
		if (pieces.Count == 0)
		{
			error = broken > 0 ? "no valid pieces (" + broken + " broken line(s))" : "no pieces";
			return false;
		}
		return true;
	}

	/// <summary>
	/// The build order: <b>by piece class first</b> (foundation/workbench -> walls, beams, poles, doors, furniture ->
	/// roofs) and, inside a class, by height. Sorting by height alone put a <c>wood_wall_roof_top_45</c> first in the
	/// player's 983-piece blueprint (its origin was below the build, so the "lowest" piece was a roof top) - measured
	/// 02/11/2026.
	/// </summary>
	internal static List<Line> Ordered(List<Line> pieces)
	{
		if (pieces == null)
		{
			return new List<Line>();
		}
		// E7 (player's report, 03/11/2026: "in this hall the 2nd floor is really the 3rd - he climbs the ladder and falls"):
		// the plan is ordered **one storey at a time**, and the storey of a piece is the highest FLOOR **near it** (3 m in
		// XZ, then 8 m) that is under it - not the highest floor of the whole building. In a double-height hall there is no
		// floor at the mid level where the ladder is, so the ladder stands on the ground floor and is placed with it.
		List<Line> floors = new List<Line>();
		foreach (Line piece in pieces)
		{
			if (piece != null && piece.Prefab != null
				&& piece.Prefab.IndexOf("floor", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				floors.Add(piece);
			}
		}
		// 04/11/2026 (the body barrier of the walk order): the LEVEL of every piece is kept on the line - it is the very
		// key the sort below uses (own height, or the slab a roof covers), never a second rule. The order does NOT change:
		// the keys below still call LevelKey itself; this pass only records the same number for the build site.
		foreach (Line piece in pieces)
		{
			if (piece != null)
			{
				piece.Level = LevelKey(floors, piece);
			}
		}
		// 05/11/2026 (o igloo medido: 44 s -> 104 s, com 86,4 s de `dependencia` e 40 desistencias do poste de emergencia,
		// todas em `wood_beam` a 3,90/4,90/7,90 m do piso): num DOMO a viga se apoia nas aguas CURVAS do telhado, que sao
		// classe 3 e vinham DEPOIS - a viga era tentada antes, recusada por `no_support` e esperava 8/12/20 s. Uma viga que
		// nao tem NADA embaixo no plano e tem telhado ao lado e construida junto do telhado.
		HashSet<Line> hangs = null;
		foreach (Line beam in pieces)
		{
			if (beam == null || beam.Prefab == null
				|| beam.Prefab.IndexOf("beam", StringComparison.OrdinalIgnoreCase) < 0
				|| ClassOf(beam.Prefab) != 2)
			{
				continue;
			}
			bool below = false;
			bool roof = false;
			foreach (Line other in pieces)
			{
				if (other == null || ReferenceEquals(other, beam))
				{
					continue;
				}
				float dx = other.Px - beam.Px;
				float dz = other.Pz - beam.Pz;
				if (dx * dx + dz * dz > 6.25f)
				{
					continue;
				}
				if (other.Py < beam.Py - 0.4f && other.Py > beam.Py - 2.5f)
				{
					below = true;
				}
				if (ClassOf(other.Prefab) == 3 && Math.Abs(other.Py - beam.Py) <= 2.5f)
				{
					roof = true;
				}
			}
			if (!below && roof)
			{
				if (hangs == null)
				{
					hangs = new HashSet<Line>();
				}
				hangs.Add(beam);
			}
		}
		List<Line> sorted = pieces
			// E7 (player's report, 03/11/2026: "he built the ground floor doors while still working on the ground floor"):
			// doors and gates are the LAST thing of the WHOLE BUILD, not of the storey - a door that exists is a door he
			// opens and closes on every trip for the rest of the work. The class 4 (doors) goes after every storey.
			.OrderBy(delegate (Line piece) { return ClassOf(piece.Prefab) == 4 ? 1 : 0; })
			// E7 (player's report, 03/11/2026: "he still tries to place those beams up top before the time"): a roof/gable piece
			// (class 3) covers the slab of the level ABOVE it, so it belongs to that storey - the sort key moves it there instead of
			// moving pieces around (the first version pulled the floor out of its storey and broke the invariant of the
			// old StoreyOf: "in this hall the 2nd floor is really the 3rd"). Review, 03/11/2026. That helper was REMOVED on
			// 05/11/2026 (review of the Claude audit): it had no caller left - `LevelKey` (below) replaced it, and the code
			// had been dead since.
			// E7 (player's report, 03/11/2026: "the site is too ugly, storey by storey would be perfect"): the key is the
			// piece's OWN height, not the floor below it. With the floor below, a wall at y=0 belonged to the storey of the floor
			// at y=-0,5 and was built BEFORE that floor; the house has levels every metre and the site came out as floating
			// floors. By own height the build walks the levels upwards: floors -> walls -> roofs of one level, then the next.
			.ThenBy(delegate (Line piece) { return LevelKey(floors, piece); })
			// a viga que pende do telhado entra com o telhado (classe 3): ver o bloco `hangs` acima
			.ThenBy(delegate (Line piece) { return hangs != null && hangs.Contains(piece) ? 3 : ClassOf(piece.Prefab); })
			.ThenBy(delegate (Line piece) { return piece.Py; })
			.ToList();
		// E7 (player's report, 03/11/2026: "he walks back and forth across the build even on the ground floor"): the four keys
		// above decided the GROUPS, but inside a group the order was the one written in the .blueprint - and that file is in the
		// order the author placed the pieces, not a path. Each group is reordered by NEAREST NEIGHBOUR from where the previous
		// group ended; no piece changes group, is added or is removed.
		NearestWithin(sorted, floors, hangs);
		return sorted;
	}

	/// <summary>
	/// E7 (player's request, 03/11/2026): within one run of pieces that share the sort keys (door flag, storey, class and
	/// height) the greedy path from where the previous run ended decides the order - the "nearest neighbour" walk. The runs
	/// themselves, the storeys, the classes and the last-place doors are untouched: this only decides WHICH piece of a group
	/// comes first. Pure, like the rest of the parser (no Unity).
	/// </summary>
	/// <summary>05/11/2026: a classe usada pelo sort E pelo tour - a viga que pende do telhado conta como 3 nos dois,
	/// senão o tour (que reagrupa) desfaz o que o sort fez.</summary>
	private static int Cls(Line piece, HashSet<Line> hangs)
	{
		return hangs != null && hangs.Contains(piece) ? 3 : ClassOf(piece.Prefab);
	}

	private static void NearestWithin(List<Line> sorted, List<Line> floors, HashSet<Line> hangs)
	{
		if (sorted == null || sorted.Count < 3)
		{
			return;
		}
		float cursorX = sorted[0].Px;
		float cursorZ = sorted[0].Pz;
		int start = 0;
		while (start < sorted.Count)
		{
			Line first = sorted[start];
			int door = ClassOf(first.Prefab) == 4 ? 1 : 0;
			float storey = LevelKey(floors, first);
			int cls = Cls(first, hangs);
			float y = first.Py;
			int end = start + 1;
			while (end < sorted.Count)
			{
				Line next = sorted[end];
				if ((ClassOf(next.Prefab) == 4 ? 1 : 0) != door || Math.Abs(LevelKey(floors, next) - storey) > 0.01f
					|| Cls(next, hangs) != cls || Math.Abs(next.Py - y) > 0.01f)
				{
					break;
				}
				end++;
			}
			int count = end - start;
			if (count > 1)
			{
				List<Line> run = sorted.GetRange(start, count);
				List<Line> path = new List<Line>(count);
				float cx = cursorX;
				float cz = cursorZ;
				while (run.Count > 0)
				{
					int best = 0;
					float bestDistance = float.MaxValue;
					for (int i = 0; i < run.Count; i++)
					{
						float dx = run[i].Px - cx;
						float dz = run[i].Pz - cz;
						float distance = dx * dx + dz * dz;
						if (distance < bestDistance)
						{
							bestDistance = distance;
							best = i;
						}
					}
					Line chosen = run[best];
					run.RemoveAt(best);
					path.Add(chosen);
					cx = chosen.Px;
					cz = chosen.Pz;
				}
				// E7 (review, 03/11/2026): the greedy walk is better on every realistic layout measured (12-23% of the original
				// path), but a pathological one is not impossible - if it comes out LONGER than the run as it was, the original
				// order stays. Three lines, and "never worse than the file" becomes a guarantee instead of a measurement.
				if (PathLength(path) <= PathLength(sorted.GetRange(start, count)))
				{
					for (int i = 0; i < count; i++)
					{
						sorted[start + i] = path[i];
					}
				}
				cursorX = sorted[end - 1].Px;
				cursorZ = sorted[end - 1].Pz;
			}
			else
			{
				cursorX = first.Px;
				cursorZ = first.Pz;
			}
			start = end;
		}
	}

	/// <summary>The length of a walk that visits the pieces in the given order (XZ only: the climbs are the same).</summary>
	private static float PathLength(List<Line> run)
	{
		float total = 0f;
		for (int i = 1; i < run.Count; i++)
		{
			float dx = run[i].Px - run[i - 1].Px;
			float dz = run[i].Pz - run[i - 1].Pz;
			total += (float)Math.Sqrt(dx * dx + dz * dz);
		}
		return total;
	}

	/// <summary>
	/// E7 (player's reports, 03/11/2026): the LEVEL of a piece is <b>its own height</b> - the build walks the levels upwards
	/// (floors, walls, roofs of one height, then the next), which is the "storey by storey" the player asked for. The old key
	/// was the floor BELOW the piece, and it put a wall at y=0 in the storey of the floor at y=-0,5 (built before it).
	/// <para>Exception: a <b>roof/gable</b> (class 3) belongs to the <b>slab it covers</b> - the nearest floor above it (0,3-6 m,
	/// 2,5 m in XZ) - or it would be built before the floor it covers.</para>
	/// </summary>
	private static float LevelKey(List<Line> floors, Line piece)
	{
		float own = (float)Math.Round(piece != null ? piece.Py : 0f, 2);
		if (piece == null || piece.Prefab == null || ClassOf(piece.Prefab) != 3 || floors == null)
		{
			return own;
		}
		float best = float.MaxValue;
		foreach (Line floor in floors)
		{
			if (floor == null)
			{
				continue;
			}
			float dy = floor.Py - piece.Py;
			if (dy < 0.3f || dy > 6f)
			{
				continue;
			}
			float dx = floor.Px - piece.Px;
			float dz = floor.Pz - piece.Pz;
			if (dx * dx + dz * dz > 6.25f)
			{
				continue;
			}
			if (dy < best)
			{
				best = dy;
			}
		}
		return best < float.MaxValue ? piece.Py + best : own;
	}

	/// <summary>
	/// 04/11/2026 (the cost of the wait of the build at 60 Hz): the answer is a function of the prefab, so it is
	/// <b>memoised</b>. This used to build the lower-cased copy of the name on <b>every</b> call (<c>ToLowerInvariant</c>
	/// allocates), and the site's queue asks it per eligible entry per frame while a storey barrier is up (through
	/// <c>MHR_BuildGroup.IsBelow</c>, from <c>NextRetryBelow</c>/<c>PendingBelow</c>), plus <c>MHR_Build.SpreadWindow</c> and
	/// <c>PullSupports</c> - thousands of strings per frame in the stuck phase, all of them identical. One entry per distinct
	/// prefab (~100 in the whole corpus), so the cache is bounded and the value stored is exactly what the rule returns.
	/// </summary>
	private static readonly Dictionary<string, int> s_classes = new Dictionary<string, int>(StringComparer.Ordinal);

	/// <summary>0 = ground (floors, foundations, the workbench), 1 = the way UP (stairs, stepladders, ladders), 2 = what
	/// stands on it (walls, beams, poles, furniture), 3 = roofs and gables, 4 = doors and gates (LAST).</summary>
	internal static int ClassOf(string prefab)
	{
		string key = prefab ?? string.Empty;
		int cached;
		if (s_classes.TryGetValue(key, out cached))
		{
			return cached;   // the same int the rule below would compute - the harness pins it per prefab
		}
		int cls = ClassOfOnce(key);
		s_classes[key] = cls;
		return cls;
	}

	/// <summary>The rule itself, computed once per prefab (<see cref="ClassOf"/> memoises the answer).
	/// <paramref name="name"/> is never null - the caller already turned null into the empty string.</summary>
	private static int ClassOfOnce(string name)
	{
		string lower = name.ToLowerInvariant();
		if (lower.IndexOf("workbench", StringComparison.Ordinal) >= 0)
		{
			return 0;      // a blueprint that carries its own station places it first (it enables the rest)
		}
		if (lower.IndexOf("floor", StringComparison.Ordinal) >= 0 || lower.IndexOf("foundation", StringComparison.Ordinal) >= 0)
		{
			return 0;
		}
		if (lower.IndexOf("stair", StringComparison.Ordinal) >= 0 || lower.IndexOf("ladder", StringComparison.Ordinal) >= 0)
		{
			return 1;
		}
		if (lower.IndexOf("roof", StringComparison.Ordinal) >= 0)
		{
			return 3;      // includes wood_wall_roof_45 / _top (a gable is roof)
		}
		if (lower.IndexOf("door", StringComparison.Ordinal) >= 0 || lower.IndexOf("gate", StringComparison.Ordinal) >= 0)
		{
			return 4;      // last: the doorway stays open while the work goes on (the templates already do this)
		}
		return 2;
	}


	private static Line ParseLine(string line)
	{
		string text = line.IndexOf(',') >= 0 ? line.Replace(',', '.') : line;
		string[] parts = text.Split(';');
		if (parts.Length < 9)
		{
			return null;
		}
		string prefab = parts[0].Trim();
		int paren = prefab.IndexOf('(');
		if (paren > 0)
		{
			prefab = prefab.Substring(0, paren);       // "wood_floor(Clone)" -> "wood_floor"
		}
		if (prefab.Length == 0)
		{
			return null;
		}
		Line piece = new Line { Prefab = prefab };
		if (!Float(parts[2], out piece.Px) || !Float(parts[3], out piece.Py) || !Float(parts[4], out piece.Pz)
			|| !Float(parts[5], out piece.Rx) || !Float(parts[6], out piece.Ry) || !Float(parts[7], out piece.Rz)
			|| !Float(parts[8], out piece.Rw))
		{
			return null;
		}
		float scale;
		if (parts.Length > 10 && Float(parts[10], out scale))
		{
			piece.Scale = scale;
		}
		return piece;
	}

	private static bool Float(string text, out float value)
	{
		return float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
	}
}
