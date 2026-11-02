using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace Companions;

/// <summary>E7: one piece of a blueprint, already in world-ready types.</summary>
internal sealed class BlueprintPiece
{
	internal string Prefab;
	internal Vector3 Pos;
	internal Quaternion Rot;
	internal float Scale = 1f;
}

/// <summary>E7: a <c>.blueprint</c> loaded from the mod's folder.</summary>
internal sealed class Blueprint
{
	/// <summary>The file it came from (absolute path).</summary>
	internal string File;
	/// <summary>The <c>#Name:</c> of the file, when it has one.</summary>
	internal string Name;
	internal readonly List<BlueprintPiece> Pieces = new List<BlueprintPiece>();

	/// <summary>What the log and the speech call it: the <c>#Name:</c>, else the file name.</summary>
	internal string Label
	{
		get
		{
			if (!string.IsNullOrEmpty(Name))
			{
				return Name;
			}
			return File != null ? Path.GetFileNameWithoutExtension(File) : "?";
		}
	}

	/// <summary>The name the player says: the file name without the extension (stable across blueprints whose
	/// <c>#Name:</c> is "Custom Name", like the ones saved from the game's own capture).</summary>
	internal string Id
	{
		get { return File != null ? Path.GetFileNameWithoutExtension(File) : Name; }
	}
}

/// <summary>
/// E7: the mod's <b>blueprint library</b> - the folder <c>BepInEx/config/AI-Companions/blueprints</c> with the
/// <c>.blueprint</c> files the player downloaded (BiomeBlueprints, valheimians) or captured in game. Reading a file
/// needs nothing from PlanBuild (the format is text - see <see cref="BuildBlueprintParser"/>); PlanBuild is only needed
/// to <i>create</i> or <i>place</i> a blueprint.
/// </summary>
internal static class BuildBlueprints
{
	internal const string Extension = ".blueprint";

	/// <summary>The folder the mod reads (and creates on first use).</summary>
	internal static string Folder()
	{
		try
		{
			string folder = Path.Combine(Paths.ConfigPath, "AI-Companions", "blueprints");
			if (!Directory.Exists(folder))
			{
				Directory.CreateDirectory(folder);
			}
			return folder;
		}
		catch (Exception e)
		{
			CompanionsPlugin.Log?.LogWarning("[Blueprint] could not create the folder: " + e.Message);
			return Path.Combine(Paths.ConfigPath, "AI-Companions", "blueprints");
		}
	}

	/// <summary>Every <c>.blueprint</c> in the folder (empty when there is none).</summary>
	internal static List<string> Files()
	{
		List<string> files = new List<string>();
		try
		{
			string folder = Folder();
			if (Directory.Exists(folder))
			{
				files.AddRange(Directory.GetFiles(folder, "*" + Extension));
			}
		}
		catch (Exception e)
		{
			CompanionsPlugin.Log?.LogWarning("[Blueprint] could not list the folder: " + e.Message);
		}
		files.Sort(StringComparer.OrdinalIgnoreCase);
		return files;
	}

	/// <summary>The names the player can say (file names without the extension), for the "I have: ..." line.</summary>
	internal static List<string> Names()
	{
		List<string> names = new List<string>();
		foreach (string file in Files())
		{
			names.Add(Path.GetFileNameWithoutExtension(file));
		}
		return names;
	}

	/// <summary>Reads one file. Returns null (and logs why) when it has no piece.</summary>
	internal static Blueprint Load(string file)
	{
		if (string.IsNullOrEmpty(file))
		{
			return null;
		}
		try
		{
			List<string> lines = new List<string>(File.ReadAllLines(file, Encoding.UTF8));
			string name;
			List<BuildBlueprintParser.Line> pieces;
			string error;
			int broken;
			if (!BuildBlueprintParser.Parse(lines, out name, out pieces, out error, out broken))
			{
				CompanionsPlugin.Log?.LogWarning("[Blueprint] " + Path.GetFileName(file) + ": " + error);
				return null;
			}
			if (broken > 0)
			{
				CompanionsPlugin.Log?.LogWarning("[Blueprint] " + Path.GetFileName(file) + ": " + broken + " broken line(s) skipped");
			}
			Blueprint blueprint = new Blueprint { File = file, Name = name };
			List<BuildBlueprintParser.Line> ordered = BuildBlueprintParser.Ordered(pieces);
			foreach (BuildBlueprintParser.Line line in ordered)
			{
				blueprint.Pieces.Add(new BlueprintPiece
				{
					Prefab = line.Prefab,
					Pos = new Vector3(line.Px, line.Py, line.Pz),
					Rot = new Quaternion(line.Rx, line.Ry, line.Rz, line.Rw).normalized,
					Scale = line.Scale
				});
			}
			return blueprint;
		}
		catch (Exception e)
		{
			CompanionsPlugin.Log?.LogWarning("[Blueprint] " + file + ": " + e.Message);
			return null;
		}
	}

	/// <summary>
	/// The blueprint the player's words name: the file name (no extension) or the <c>#Name:</c>, whole words and
	/// accent-insensitive (the same rule as the piece catalog, so "casa" cannot swallow "casaviking"). The longest
	/// match wins; null when nothing matches.
	/// </summary>
	internal static Blueprint Resolve(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}
		string haystack = " " + BuildCatalog.Fold(text) + " ";
		Blueprint best = null;
		int bestLength = 0;
		foreach (string file in Files())
		{
			string id = Path.GetFileNameWithoutExtension(file);
			Blueprint candidate = Load(file);
			if (candidate == null)
			{
				continue;
			}
			foreach (string name in new string[] { id, candidate.Name })
			{
				if (string.IsNullOrEmpty(name))
				{
					continue;
				}
				string needle = " " + BuildCatalog.Fold(name) + " ";
				if (needle.Length > bestLength && haystack.IndexOf(needle, StringComparison.Ordinal) >= 0)
				{
					best = candidate;
					bestLength = needle.Length;
				}
			}
		}
		return best;
	}
}
