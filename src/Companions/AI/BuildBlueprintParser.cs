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

	/// <summary>Bottom-up order: by height, then the original order (stable) - the ground pieces first, the roof last.</summary>
	internal static List<Line> Ordered(List<Line> pieces)
	{
		if (pieces == null)
		{
			return new List<Line>();
		}
		return pieces.OrderBy(delegate (Line piece) { return piece.Py; }).ToList();
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
