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
		List<Line> sorted = pieces
			// E7 (player's report, 03/11/2026: "he built the ground floor doors while still working on the ground floor"):
			// doors and gates are the LAST thing of the WHOLE BUILD, not of the storey - a door that exists is a door he
			// opens and closes on every trip for the rest of the work. The class 4 (doors) goes after every storey.
			.OrderBy(delegate (Line piece) { return ClassOf(piece.Prefab) == 4 ? 1 : 0; })
			// E7 (player's report, 03/11/2026: "he still tries to place those beams up top before the time"): a roof/gable piece
			// (class 3) covers the slab of the level ABOVE it, so it belongs to that storey - the sort key moves it there instead of
			// moving pieces around (the first version pulled the floor out of its storey and broke the invariant the StoreyOf exists
			// for: "in this hall the 2nd floor is really the 3rd"). Review, 03/11/2026.
			// E7 (player's report, 03/11/2026: "the site is too ugly, storey by storey would be perfect"): the key is the
			// piece's OWN height, not the floor below it. With the floor below, a wall at y=0 belonged to the storey of the floor
			// at y=-0,5 and was built BEFORE that floor; the house has levels every metre and the site came out as floating
			// floors. By own height the build walks the levels upwards: floors -> walls -> roofs of one level, then the next.
			.ThenBy(delegate (Line piece) { return LevelKey(floors, piece); })
			.ThenBy(delegate (Line piece) { return ClassOf(piece.Prefab); })
			.ThenBy(delegate (Line piece) { return piece.Py; })
			.ToList();
		// E7 (player's report, 03/11/2026: "he walks back and forth across the build even on the ground floor"): the four keys
		// above decided the GROUPS, but inside a group the order was the one written in the .blueprint - and that file is in the
		// order the author placed the pieces, not a path. Each group is reordered by NEAREST NEIGHBOUR from where the previous
		// group ended; no piece changes group, is added or is removed.
		NearestWithin(sorted, floors);
		return sorted;
	}

	/// <summary>
	/// E7 (player's request, 03/11/2026): within one run of pieces that share the sort keys (door flag, storey, class and
	/// height) the greedy path from where the previous run ended decides the order - the "nearest neighbour" walk. The runs
	/// themselves, the storeys, the classes and the last-place doors are untouched: this only decides WHICH piece of a group
	/// comes first. Pure, like the rest of the parser (no Unity).
	/// </summary>
	private static void NearestWithin(List<Line> sorted, List<Line> floors)
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
			int cls = ClassOf(first.Prefab);
			float y = first.Py;
			int end = start + 1;
			while (end < sorted.Count)
			{
				Line next = sorted[end];
				if ((ClassOf(next.Prefab) == 4 ? 1 : 0) != door || Math.Abs(LevelKey(floors, next) - storey) > 0.01f
					|| ClassOf(next.Prefab) != cls || Math.Abs(next.Py - y) > 0.01f)
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

	/// <summary>0 = ground (floors, foundations, the workbench), 1 = the way UP (stairs, stepladders, ladders), 2 = what
	/// stands on it (walls, beams, poles, furniture), 3 = roofs and gables, 4 = doors and gates (LAST).</summary>
	internal static int ClassOf(string prefab)
	{
		string name = (prefab ?? string.Empty).ToLowerInvariant();
		if (name.IndexOf("workbench", StringComparison.Ordinal) >= 0)
		{
			return 0;      // a blueprint that carries its own station places it first (it enables the rest)
		}
		if (name.IndexOf("floor", StringComparison.Ordinal) >= 0 || name.IndexOf("foundation", StringComparison.Ordinal) >= 0)
		{
			return 0;
		}
		if (name.IndexOf("stair", StringComparison.Ordinal) >= 0 || name.IndexOf("ladder", StringComparison.Ordinal) >= 0)
		{
			return 1;
		}
		if (name.IndexOf("roof", StringComparison.Ordinal) >= 0)
		{
			return 3;      // includes wood_wall_roof_45 / _top (a gable is roof)
		}
		if (name.IndexOf("door", StringComparison.Ordinal) >= 0 || name.IndexOf("gate", StringComparison.Ordinal) >= 0)
		{
			return 4;      // last: the doorway stays open while the work goes on (the templates already do this)
		}
		return 2;
	}

	/// <summary>
	/// The height of the floor a piece stands on: the highest floor under it within 3 m in XZ (a player builds around him),
	/// then within 8 m, and only as a last resort the highest floor anywhere below - that last one is what made the hall of
	/// the player's blueprint put the ladder at a level with no floor (<c>falling off the ladder in a loop</c>).
	/// </summary>
	internal static float StoreyOf(List<Line> floors, Line piece)
	{
		if (floors == null || floors.Count == 0 || piece == null)
		{
			return 0f;
		}
		float near = float.MinValue;
		float mid = float.MinValue;
		float anywhere = float.MinValue;
		foreach (Line floor in floors)
		{
			if (floor == null || floor.Py > piece.Py + 0.6f)
			{
				continue;
			}
			if (floor.Py > anywhere)
			{
				anywhere = floor.Py;
			}
			float dx = floor.Px - piece.Px;
			float dz = floor.Pz - piece.Pz;
			float square = dx * dx + dz * dz;
			if (square <= 9f && floor.Py > near)
			{
				near = floor.Py;          // 3 m
			}
			if (square <= 64f && floor.Py > mid)
			{
				mid = floor.Py;           // 8 m
			}
		}
		if (near > float.MinValue)
		{
			return near;
		}
		if (mid > float.MinValue)
		{
			return mid;
		}
		return anywhere > float.MinValue ? anywhere : 0f;
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
