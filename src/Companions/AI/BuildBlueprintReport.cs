using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Companions;

/// <summary>
/// E7: <b>one</b> <c>.blueprint</c> read the way the order reads it, as plain numbers - the same parser
/// (<see cref="BuildBlueprintParser"/>, with the <c>Ordered</c> pass that fills the level of every piece), the same
/// prefab resolution (<see cref="BuildProbe.PieceOf"/>) and the same skip rules (<see cref="BuildBlueprintSkip"/>) as
/// <c>MHR_Build.StartBlueprint</c>, <b>without starting anything</b>.
///
/// <para>It was the body of the DevBridge's <c>blueprints.inspect</c>; it lives here so the build panel (which shows the
/// plan and its material need before the order) and the bridge answer from the <b>same</b> code - a second reader
/// would be a second set of rules. The fields are plain (floats, ints, dictionaries): the caller turns them into JSON,
/// and <see cref="Materials"/> is the whole plan's need (<see cref="BuildMaterials"/>), the number a player wants to
/// hear before saying "build it".</para>
///
/// <para>Unity is needed for the pieces themselves (<c>BuildProbe.PieceOf</c> resolves the game's prefabs), so the world
/// has to be loaded - the caller checks that, as the bridge always did.</para>
/// </summary>
internal sealed class BuildBlueprintReport
{
	/// <summary>One group of skipped lines: the same prefab may be left out for two different reasons.</summary>
	internal sealed class SkipReason
	{
		internal string Prefab;
		internal string Reason;
		internal int Count;
	}

	/// <summary>The file name (not the full path - the caller knows the path it asked for).</summary>
	internal string File;
	/// <summary>The <c>#Name:</c> of the file (null when the file has none).</summary>
	internal string Name;
	/// <summary>How many lines the file has (all of them, headers included).</summary>
	internal int Lines;
	/// <summary>How many lines the parser accepted as pieces (broken lines are not here).</summary>
	internal int PieceLines;
	/// <summary>Lines that looked like pieces but could not be read.</summary>
	internal int Broken;
	/// <summary>The pieces of the plan (what <c>MHR_Build.StartBlueprint</c> would build).</summary>
	internal int Pieces;
	/// <summary>The lines left out by <see cref="BuildBlueprintSkip"/>.</summary>
	internal int Skipped;
	/// <summary>Pieces whose <c>m_craftingStation</c> is not null (they need a station in range to be placed).</summary>
	internal int PiecesNeedingStation;
	/// <summary>Distinct heights of the plan's floor slabs (a file with no floor is one storey).</summary>
	internal int Storeys;
	/// <summary>Whether <see cref="Pieces"/> &gt; 0 (a file with no piece has no bounds at all).</summary>
	internal bool HasBounds;
	internal float MinX;
	internal float MinY;
	internal float MinZ;
	internal float MaxX;
	internal float MaxY;
	internal float MaxZ;
	/// <summary>Prefab -&gt; how many pieces of it the plan builds (the order <c>Ordered</c> walks).</summary>
	internal Dictionary<string, int> Prefabs;
	/// <summary>Station token -&gt; how many lines of the file need it (a skipped line counts too).</summary>
	internal Dictionary<string, int> Stations;
	/// <summary>Why the skipped lines were left out, grouped by prefab and reason, in the order the plan walks them.</summary>
	internal List<SkipReason> SkippedReasons;
	/// <summary>The whole plan's material need: shared item name -&gt; how many.</summary>
	internal Dictionary<string, int> Materials;
	/// <summary>How many prefabs of <see cref="Prefabs"/> answered no cost at all (no recipe here).</summary>
	internal int NoRecipe;

	/// <summary>
	/// Reads <paramref name="file"/> and fills <paramref name="report"/>. False with <paramref name="error"/> when the
	/// file cannot be read or the parser finds no piece at all (the messages the bridge already answered: the read
	/// exception's own text, or <c>BuildBlueprintParser.Parse</c>'s). The caller resolves the path and checks that the
	/// world is loaded - the report cannot resolve a prefab without it.
	/// </summary>
	internal static bool Read(string file, out BuildBlueprintReport report, out string error)
	{
		report = null;
		error = null;
		string[] lines;
		try
		{
			// System.IO on purpose: the report has a FIELD called File (the file name) and it would shadow the type
			lines = System.IO.File.ReadAllLines(file, Encoding.UTF8);
		}
		catch (Exception e)
		{
			error = e.Message;
			return false;
		}
		string name;
		List<BuildBlueprintParser.Line> parsed;
		int broken;
		if (!BuildBlueprintParser.Parse(lines, out name, out parsed, out error, out broken))
		{
			return false;
		}
		// the VERY pass the order runs: Ordered() decides the walk and fills Line.Level, the key this report counts as a
		// level of the plan (never a second rule)
		List<BuildBlueprintParser.Line> ordered = BuildBlueprintParser.Ordered(parsed);
		Player local = Player.m_localPlayer;
		BuildBlueprintReport result = new BuildBlueprintReport
		{
			File = System.IO.Path.GetFileName(file),
			Name = name,
			Lines = lines.Length,
			PieceLines = parsed.Count,
			Broken = broken,
			Prefabs = new Dictionary<string, int>(),
			Stations = new Dictionary<string, int>(),
			SkippedReasons = new List<SkipReason>(),
			Materials = new Dictionary<string, int>()
		};
		Dictionary<string, int> skipAt = new Dictionary<string, int>();
		HashSet<int> floorLevels = new HashSet<int>();
		// one lookup per prefab: a blueprint repeats them by the hundred and the fallback of BuildProbe.PieceOf scans
		// every prefab of the game on a case mismatch. A null answer is cached too (it is the "not a piece here").
		Dictionary<string, Piece> resolvedCache = new Dictionary<string, Piece>(StringComparer.Ordinal);
		float minX = float.MaxValue;
		float minY = float.MaxValue;
		float minZ = float.MaxValue;
		float maxX = float.MinValue;
		float maxY = float.MinValue;
		float maxZ = float.MinValue;
		foreach (BuildBlueprintParser.Line line in ordered)
		{
			Piece resolved;
			if (!resolvedCache.TryGetValue(line.Prefab, out resolved))
			{
				resolved = BuildProbe.PieceOf(line.Prefab);
				resolvedCache[line.Prefab] = resolved;
			}
			if (resolved != null && resolved.m_craftingStation != null)
			{
				// the station answers "which stations does this FILE need", so a line skipped by the recipe counts too
				// (the 2x stone_floor_2x2 under no stonecutter are the measured case)
				Count(result.Stations, resolved.m_craftingStation.m_name);
			}
			string skip = BuildBlueprintSkip.Reason(
				resolved != null,
				local == null || (resolved != null && local.IsRecipeKnown(resolved.m_name)));
			if (skip != null)
			{
				result.Skipped++;
				CountSkip(result.SkippedReasons, skipAt, line.Prefab, skip);
				continue;
			}
			result.Pieces++;
			if (resolved.m_craftingStation != null)
			{
				result.PiecesNeedingStation++;
			}
			Count(result.Prefabs, line.Prefab);
			minX = Mathf.Min(minX, line.Px);
			minY = Mathf.Min(minY, line.Py);
			minZ = Mathf.Min(minZ, line.Pz);
			maxX = Mathf.Max(maxX, line.Px);
			maxY = Mathf.Max(maxY, line.Py);
			maxZ = Mathf.Max(maxZ, line.Pz);
			if (line.Prefab.IndexOf("floor", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				// a storey is a floor slab: the distinct heights of the floors of the plan, 0,1 m apart (the parser's
				// own "floor" test, the same one Ordered() uses to find the slabs a roof covers)
				floorLevels.Add((int)Math.Round(line.Py * 10f));
			}
		}
		if (result.Pieces > 0)
		{
			result.HasBounds = true;
			result.MinX = minX;
			result.MinY = minY;
			result.MinZ = minZ;
			result.MaxX = maxX;
			result.MaxY = maxY;
			result.MaxZ = maxZ;
		}
		// a file with no floor at all (posts and a roof) is still one storey; a file with no piece at all has none
		result.Storeys = result.Pieces == 0 ? 0 : (floorLevels.Count > 0 ? floorLevels.Count : 1);
		// the material need of the WHOLE plan, from the very prefabs the walk resolved (the same costs BuildStock pays
		// per piece: the resolver only reads Piece.m_resources)
		result.Materials = BuildMaterials.Total(result.Prefabs,
			delegate(string prefab) { return CostsOf(prefab, resolvedCache); }, out result.NoRecipe);
		report = result;
		return true;
	}

	/// <summary>
	/// The costs of one prefab of the plan, as <see cref="BuildMaterials"/> counts them: the prefab is resolved (and
	/// cached - a blueprint repeats its prefabs by the hundred) and the rules live in <see cref="BuildStock.CostsOf"/>,
	/// the same ones the order pays with.
	/// </summary>
	private static IReadOnlyList<Cost> CostsOf(string prefab, Dictionary<string, Piece> cache)
	{
		Piece piece;
		if (cache == null || !cache.TryGetValue(prefab, out piece))
		{
			piece = BuildProbe.PieceOf(prefab);
			if (cache != null)
			{
				cache[prefab] = piece;
			}
		}
		return BuildStock.CostsOf(piece);
	}

	private static void Count(Dictionary<string, int> counts, string key)
	{
		if (string.IsNullOrEmpty(key))
		{
			return;
		}
		int have;
		counts.TryGetValue(key, out have);
		counts[key] = have + 1;
	}

	/// <summary>Groups the skipped lines by prefab AND reason (the same prefab may be left out for two reasons), in the
	/// order the plan walks them.</summary>
	private static void CountSkip(List<SkipReason> entries, Dictionary<string, int> at, string prefab, string reason)
	{
		string key = prefab + "\n" + reason;
		int index;
		if (at.TryGetValue(key, out index))
		{
			entries[index].Count++;
			return;
		}
		at[key] = entries.Count;
		entries.Add(new SkipReason { Prefab = prefab, Reason = reason, Count = 1 });
	}
}
