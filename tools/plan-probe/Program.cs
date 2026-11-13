using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Companions;

/// <summary>
/// Reads a real .blueprint through the mod's own parser (BuildBlueprintParser.cs is compiled from src/, nothing is
/// copied) and prints the plan in the order the build walks it. This is the instrument that answers "does the plan go
/// bottom to top" with the plan's own numbers instead of an opinion about the code.
/// </summary>
internal static class Program
{
	private static int Main(string[] args)
	{
		if (args.Length < 1)
		{
			Console.WriteLine("usage: PlanProbe <file.blueprint> [--dump]");
			return 2;
		}
		string name;
		List<BuildBlueprintParser.Line> pieces;
		string error;
		int broken;
		if (!BuildBlueprintParser.Parse(File.ReadAllLines(args[0]), out name, out pieces, out error, out broken))
		{
			Console.WriteLine("parse failed: " + error);
			return 2;
		}
		List<BuildBlueprintParser.Line> plan = BuildBlueprintParser.Ordered(pieces);
		Console.WriteLine("name: " + name + "  lines: " + pieces.Count + "  broken: " + broken + "  plan: " + plan.Count);

		// 1. level and height monotonicity
		int levelDrops = 0, yDrops = 0;
		float lastLevel = float.MinValue, lastY = float.MinValue;
		for (int i = 0; i < plan.Count; i++)
		{
			if (plan[i].Level < lastLevel - 0.01f)
			{
				levelDrops++;
				if (levelDrops <= 5)
				{
					Console.WriteLine("  LEVEL DROP at " + i + ": " + F(lastLevel) + " -> " + F(plan[i].Level)
						+ " (" + plan[i].Prefab + ")");
				}
			}
			lastLevel = Math.Max(lastLevel, plan[i].Level);
			if (plan[i].Py < lastY - 0.01f && plan[i].Level >= lastLevel - 0.01f)
			{
				yDrops++;
			}
			lastY = plan[i].Py;
		}
		Console.WriteLine("level drops: " + levelDrops + "   y drops inside a level: " + yDrops);

		// 2. per level: how many pieces, and the horizontal spread of the FIRST half vs the LAST half
		Console.WriteLine();
		Console.WriteLine("level      n   class histogram        first-half centre        last-half centre");
		int start = 0;
		while (start < plan.Count)
		{
			int end = start + 1;
			while (end < plan.Count && Math.Abs(plan[end].Level - plan[start].Level) < 0.01f)
			{
				end++;
			}
			int n = end - start;
			Dictionary<int, int> classes = new Dictionary<int, int>();
			for (int i = start; i < end; i++)
			{
				int c = BuildBlueprintParser.ClassOf(plan[i].Prefab);
				classes[c] = (classes.TryGetValue(c, out int had) ? had : 0) + 1;
			}
			string hist = "";
			foreach (KeyValuePair<int, int> pair in classes)
			{
				hist += "c" + pair.Key + "=" + pair.Value + " ";
			}
			float ax, az, bx, bz;
			Centre(plan, start, start + Math.Max(1, n / 2), out ax, out az);
			Centre(plan, start + n / 2, end, out bx, out bz);
			Console.WriteLine(F(plan[start].Level) + "  " + n.ToString().PadLeft(4) + "   " + hist.PadRight(22)
				+ "  (" + F(ax) + ", " + F(az) + ")" + new string(' ', 6) + "(" + F(bx) + ", " + F(bz) + ")");
			start = end;
		}

		if (Array.IndexOf(args, "--dump") >= 0)
		{
			Console.WriteLine();
			Console.WriteLine("index prefab                     level    y        x        z   class");
			for (int i = 0; i < plan.Count; i++)
			{
				Console.WriteLine((i + 1).ToString().PadLeft(5) + " " + plan[i].Prefab.PadRight(28)
					+ F(plan[i].Level).PadLeft(7) + F(plan[i].Py).PadLeft(8)
					+ F(plan[i].Px).PadLeft(9) + F(plan[i].Pz).PadLeft(9)
					+ BuildBlueprintParser.ClassOf(plan[i].Prefab).ToString().PadLeft(5));
			}
		}
		return 0;
	}

	private static void Centre(List<BuildBlueprintParser.Line> plan, int from, int to, out float x, out float z)
	{
		x = 0f;
		z = 0f;
		if (to <= from)
		{
			return;
		}
		for (int i = from; i < to; i++)
		{
			x += plan[i].Px;
			z += plan[i].Pz;
		}
		x /= to - from;
		z /= to - from;
	}

	private static string F(float value)
	{
		return value.ToString("0.00", CultureInfo.InvariantCulture);
	}
}
