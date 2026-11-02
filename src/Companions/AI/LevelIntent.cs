using System;
using System.Collections.Generic;
using System.Text;

namespace Companions;

/// <summary>
/// Fase E10: the <b>leveling order</b> - "nivele um terreno 10x10 aqui". It is <b>pure</b> (no Unity, no world), so the
/// offline harness can link it: the verb and the size are read from the player's own words, in the five languages,
/// because the model does not carry a size field.
///
/// <para>Two questions: is this a leveling request (<see cref="IsRequest"/>), and how big is the area
/// (<see cref="Size"/>)? "nivele um terreno 10x10 aqui", "nivela o chão 6x6", "level the ground 12 by 12" and
/// "aplana 8 m de terreno" all answer 10/6/12/8. A request with no size uses the fallback (8 m).</para>
/// </summary>
internal static class LevelIntent
{
	/// <summary>The verbs that ask for leveling (folded, matched as whole words).</summary>
	private static readonly string[] Verbs =
	{
		"nivele", "nivela", "nivelar", "nivelamento", "nivelado",
		"aplane", "aplana", "aplanar", "alise", "alisar",
		"level", "flatten", "flattening", "smooth", "grade",
		"nivela", "nivelar", "allana", "allanar", "aplana",
		"nivelle", "niveler", "aplanir", "aplatir",
		"livella", "livellare", "appiana", "appianare", "spiana"
	};

	/// <summary>The nouns that make it about the ground (a verb alone is enough when a size comes with it).</summary>
	private static readonly string[] Nouns =
	{
		"terreno", "chao", "solo", "area", "terrain", "ground", "land", "floor",
		"suelo", "terreno", "area", "terrain", "sol", "terrain", "terreno", "area", "suolo", "terra"
	};

	/// <summary>Is the message a leveling request? The verb alone is enough ("nivele aí"): "nivelar", "level", "flatten",
	/// "aplatir" are only about the ground in this game.</summary>
	internal static bool IsRequest(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return false;
		}
		return BuildCatalog.HasAnyWord(text, Verbs);
	}

	/// <summary>
	/// The side of the area, in metres, read from the text: "10x10", "6 x 6", "10 por 10", "12 by 12", "8m", "8 metros",
	/// "10 de lado". 0 when the text has no size (the caller uses its default).
	/// </summary>
	internal static float Size(string text, float fallback)
	{
		if (string.IsNullOrEmpty(text))
		{
			return fallback;
		}
		string folded = BuildCatalog.Fold(text);
		string[] parts = folded.Split(' ');
		string[] units = { "metros", "meters", "metri", "metro", "meter", "m" };
		for (int i = 0; i < parts.Length; i++)
		{
			string part = parts[i];
			// "10x10" / "10x"
			int x = part.IndexOf('x');
			if (x > 0)
			{
				float left;
				if (TryNumber(part.Substring(0, x), out left) && left >= 2f && left <= 40f)
				{
					return left;
				}
			}
			// "8m" / "12metros" / "10meters"
			foreach (string unit in units)
			{
				if (part.Length > unit.Length && part.EndsWith(unit))
				{
					float glued;
					if (TryNumber(part.Substring(0, part.Length - unit.Length), out glued) && glued >= 2f && glued <= 40f)
					{
						return glued;
					}
				}
			}
			// "10 por 10" / "12 by 12" / "8 metros"
			float number;
			if (TryNumber(part, out number) && number >= 2f && number <= 40f && i + 1 < parts.Length)
			{
				string next = parts[i + 1];
				bool pair = next == "por" || next == "by" || next == "x";
				bool unitWord = Array.IndexOf(units, next) >= 0;
				if (pair || unitWord)
				{
					return number;
				}
			}
		}
		return fallback;
	}

	private static bool TryNumber(string text, out float value)
	{
		value = 0f;
		if (string.IsNullOrEmpty(text))
		{
			return false;
		}
		// a plain integer, optionally with a decimal part ("10", "12.5")
		bool any = false;
		StringBuilder clean = new StringBuilder();
		foreach (char c in text)
		{
			if (c >= '0' && c <= '9')
			{
				clean.Append(c);
				any = true;
			}
			else if (c == '.' || c == ',')
			{
				clean.Append('.');
			}
			else
			{
				return false;
			}
		}
		if (!any)
		{
			return false;
		}
		return float.TryParse(clean.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
	}
}
