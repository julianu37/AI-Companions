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

	// The folder listing is cached for a few seconds: a chat order resolves a name on every message, and a library of
	// 353 files (BiomeBlueprints) must not cost a directory scan each time.
	private static List<string> s_cache;
	private static float s_cacheAt = -60f;
	private const float CacheSeconds = 10f;

	/// <summary>Every <c>.blueprint</c> in the folder (empty when there is none).</summary>
	internal static List<string> Files()
	{
		if (s_cache != null && Time.realtimeSinceStartup - s_cacheAt < CacheSeconds)
		{
			return new List<string>(s_cache);
		}
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
		s_cache = files;
		s_cacheAt = Time.realtimeSinceStartup;
		return new List<string>(files);
	}

	/// <summary>Forgets the cached listing (a file dropped in the folder is seen at once).</summary>
	internal static void Invalidate()
	{
		s_cache = null;
		s_cacheAt = -60f;
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

	/// <summary>
	/// E7 (review, 03/11/2026): the last file read, by path and write time. <see cref="Load"/> is called from
	/// <c>MHR_Build.CheckPending</c> on EVERY FRAME while a blueprint order waits for its material, and since the order is
	/// rebuilt (parse + sort) that put a 985-line read and an O(n^2) sort on the main thread every frame. The file hardly
	/// changes, so the parsed plan is kept until the write time changes.
	/// </summary>
	private static string s_cachedPath;
	private static DateTime s_cachedWrite;
	private static Blueprint s_cachedBlueprint;

	/// <summary>Reads one file. Returns null (and logs why) when it has no piece.</summary>
	internal static Blueprint Load(string file)
	{
		if (string.IsNullOrEmpty(file))
		{
			return null;
		}
		try
		{
			DateTime write = File.GetLastWriteTimeUtc(file);
			if (s_cachedBlueprint != null && s_cachedPath == file && s_cachedWrite == write)
			{
				return s_cachedBlueprint;
			}
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
			s_cachedPath = file;
			s_cachedWrite = write;
			s_cachedBlueprint = blueprint;
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
		int matched;
		return Resolve(text, out matched);
	}

	/// <summary>The same resolution, also answering how many characters of the text were matched (see
	/// <see cref="MHR_Build"/>'s note: the longest name wins between a template and a blueprint).</summary>
	internal static Blueprint Resolve(string text, out int matched)
	{
		matched = 0;
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}
		string haystack = " " + BuildCatalog.Fold(text) + " ";
		List<string> files = Files();
		// 1) the FILE NAME first, without loading anything: it is what the order says ("construa a hs_meadows_hut")
		string bestFile = null;
		int bestLength = 0;
		foreach (string file in files)
		{
			string needle = " " + BuildCatalog.Fold(Path.GetFileNameWithoutExtension(file)) + " ";
			if (needle.Length > bestLength && haystack.IndexOf(needle, StringComparison.Ordinal) >= 0)
			{
				bestFile = file;
				bestLength = needle.Length;
			}
		}
		// 2) only for a small library, the #Name: inside the file (reading all 353 files of a big library on every
		// message would cost more than it is worth - the file name is the documented way to ask)
		if (bestFile == null && files.Count <= 25)
		{
			foreach (string file in files)
			{
				Blueprint candidate = Load(file);
				if (candidate == null || string.IsNullOrEmpty(candidate.Name))
				{
					continue;
				}
				string needle = " " + BuildCatalog.Fold(candidate.Name) + " ";
				if (needle.Length > bestLength && haystack.IndexOf(needle, StringComparison.Ordinal) >= 0)
				{
					bestFile = file;
					bestLength = needle.Length;
				}
			}
		}
		matched = bestLength;
		return bestFile != null ? Load(bestFile) : null;
	}
}
