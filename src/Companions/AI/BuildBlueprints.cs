using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
	/// <summary>04/11/2026 (the body barrier): the storey of the piece (<c>BuildBlueprintParser.Line.Level</c>) - the
	/// build site holds back only the pieces above the storey a body is standing on.</summary>
	internal float Level;
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
/// E7: the mod's <b>blueprint library</b> - the folder <c>BepInEx/plugins/AI-Companions/blueprints</c> (next to the DLL,
/// like <c>tts\</c> and <c>whisper\</c>) with the <c>.blueprint</c> files the player downloaded (BiomeBlueprints,
/// valheimians) or captured in game. It is <i>content</i>, not config, so the package can ship it; the old
/// <c>BepInEx/config/AI-Companions/blueprints</c> is copied over once (04/11/2026). Reading a file
/// needs nothing from PlanBuild (the format is text - see <see cref="BuildBlueprintParser"/>); PlanBuild is only needed
/// to <i>create</i> or <i>place</i> a blueprint.
/// </summary>
internal static class BuildBlueprints
{
	internal const string Extension = ".blueprint";

	/// <summary>04/11/2026: the one-shot copy of the old folder was already tried this session (the log must not
	/// repeat on every <see cref="Folder"/> call).</summary>
	private static bool s_migrated;

	/// <summary>The folder the mod reads (and creates on first use).</summary>
	internal static string Folder()
	{
		// the plugin folder is where the mod loads everything from (tts\, whisper\, Translations\): the same
		// Assembly.Location lookup CompanionTts/WhisperStt use
		string folder = Path.Combine(PluginDirectory(), "blueprints");
		try
		{
			// the migration only happens while the new folder still does not exist: a library the player already put
			// there is never touched (the copy also skips any name that is already in it)
			bool existed = Directory.Exists(folder);
			if (!existed)
			{
				Directory.CreateDirectory(folder);
				MigrateLegacy(folder);
			}
			return folder;
		}
		catch (Exception e)
		{
			CompanionsPlugin.Log?.LogWarning("[Blueprint] could not create the folder: " + e.Message);
			return folder;
		}
	}

	/// <summary>The folder of <c>Companions.dll</c> (where <c>tts\</c>, <c>whisper\</c> and <c>Translations\</c> live).</summary>
	private static string PluginDirectory()
	{
		try
		{
			string location = Assembly.GetExecutingAssembly().Location;
			if (!string.IsNullOrEmpty(location))
			{
				string dir = Path.GetDirectoryName(location);
				if (!string.IsNullOrEmpty(dir))
				{
					return dir;
				}
			}
		}
		catch (Exception e)
		{
			CompanionsPlugin.Log?.LogWarning("[Build] não consegui descobrir a pasta do plugin pelo Assembly.Location: " + e.Message);
		}
		return Directory.GetCurrentDirectory();
	}

	/// <summary>The folder the library lived in before 04/11/2026 (under the config folder).</summary>
	private static string LegacyFolder()
	{
		return Path.Combine(Paths.ConfigPath, "AI-Companions", "blueprints");
	}

	/// <summary>
	/// 04/11/2026 (player's request): copies the old library (<see cref="LegacyFolder"/>) into the new one, once. It is a
	/// COPY, never a move - if anything goes wrong the original is still there - and a file that already exists in the new
	/// folder is never overwritten (it stays and is counted as "already there"). Nothing is logged when the old folder does
	/// not exist (the normal case of a fresh install).
	/// </summary>
	private static void MigrateLegacy(string folder)
	{
		if (s_migrated)
		{
			return;
		}
		s_migrated = true;
		try
		{
			string legacy = LegacyFolder();
			if (!Directory.Exists(legacy))
			{
				return;
			}
			string[] files = Directory.GetFiles(legacy, "*" + Extension);
			if (files.Length == 0)
			{
				return;
			}
			Array.Sort(files, StringComparer.OrdinalIgnoreCase);
			int copied = 0;
			int existing = 0;
			foreach (string file in files)
			{
				string target = Path.Combine(folder, Path.GetFileName(file));
				if (File.Exists(target))
				{
					existing++;
					continue;
				}
				try
				{
					File.Copy(file, target, false);
					copied++;
				}
				catch (Exception e)
				{
					CompanionsPlugin.Log?.LogWarning("[Build] blueprints: não consegui copiar " + Path.GetFileName(file) + " de config: " + e.Message);
				}
			}
			if (copied == 0 && existing == 0)
			{
				return;
			}
			if (CompanionsPlugin.LogNormal)
			{
				string line = "[Build] blueprints: " + copied + " arquivo(s) migrados de config para plugins";
				if (existing > 0)
				{
					line += " (" + existing + " já existia(m) na pasta nova e foi(ram) mantido(s))";
				}
				CompanionsPlugin.Log?.LogInfo(line);
			}
		}
		catch (Exception e)
		{
			CompanionsPlugin.Log?.LogWarning("[Build] blueprints: falha ao migrar de config para plugins: " + e.Message);
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
	/// E7 (review, 03/11/2026): the parsed plans, by path and write time. <see cref="Load"/> is called from
	/// <c>MHR_Build.CheckPending</c> on EVERY FRAME while a blueprint order waits for its material, and since the order is
	/// rebuilt (parse + sort) that put a 985-line read and an O(n^2) sort on the main thread every frame. A small MAP, not
	/// one slot: two companions waiting for different files used to evict each other and reparse both every frame.
	/// </summary>
	private sealed class PlanCache
	{
		internal DateTime Write;
		internal Blueprint Plan;
	}
	private static readonly Dictionary<string, PlanCache> s_plans = new Dictionary<string, PlanCache>();

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
			PlanCache cached;
			if (s_plans.TryGetValue(file, out cached) && cached != null && cached.Write == write)
			{
				return cached.Plan;
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
					Scale = line.Scale,
					Level = line.Level
				});
			}
			// a small map, not one slot: two companions waiting for DIFFERENT blueprints used to evict each other and
			// reparse both every frame (review, 03/11/2026)
			if (s_plans.Count > 4)
			{
				s_plans.Clear();
			}
			s_plans[file] = new PlanCache { Write = write, Plan = blueprint };
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
