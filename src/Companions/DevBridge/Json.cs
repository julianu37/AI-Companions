using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Companions.DevBridge;

/// <summary>
/// Minimal JSON reader/writer. The bridge must not depend on System.Text.Json (it does not run inside the mod's
/// assembly and Unity's Mono does not ship System.Web.Extensions), so this is the whole protocol: objects, arrays,
/// strings, numbers, booleans and null.
/// </summary>
internal static class Json
{
	public static object Parse(string text)
	{
		int i = 0;
		object value = ParseValue(text, ref i);
		SkipWs(text, ref i);
		if (i < text.Length)
		{
			throw new FormatException("trailing characters at " + i);
		}
		return value;
	}

	public static string Serialize(object value)
	{
		StringBuilder sb = new StringBuilder();
		Write(sb, value);
		return sb.ToString();
	}

	public static string Quote(string s)
	{
		StringBuilder sb = new StringBuilder();
		WriteString(sb, s);
		return sb.ToString();
	}

	public static Dictionary<string, object> Obj(object o)
	{
		return o as Dictionary<string, object>;
	}

	public static string Str(Dictionary<string, object> d, string key, string fallback = null)
	{
		if (d != null && d.TryGetValue(key, out object v) && v is string s)
		{
			return s;
		}
		return fallback;
	}

	public static int Int(Dictionary<string, object> d, string key, int fallback = 0)
	{
		if (d != null && d.TryGetValue(key, out object v))
		{
			return ToInt(v, fallback);
		}
		return fallback;
	}

	public static float Float(Dictionary<string, object> d, string key, float fallback = 0f)
	{
		if (d != null && d.TryGetValue(key, out object v))
		{
			return ToFloat(v, fallback);
		}
		return fallback;
	}

	public static bool Bool(Dictionary<string, object> d, string key, bool fallback = false)
	{
		if (d != null && d.TryGetValue(key, out object v) && v is bool b)
		{
			return b;
		}
		return fallback;
	}

	private static int ToInt(object v, int fallback)
	{
		if (v == null)
		{
			return fallback;
		}
		if (v is long l)
		{
			return (int)l;
		}
		if (v is double d)
		{
			return (int)d;
		}
		return int.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out int r) ? r : fallback;
	}

	private static float ToFloat(object v, float fallback)
	{
		if (v == null)
		{
			return fallback;
		}
		if (v is long l)
		{
			return l;
		}
		if (v is double d)
		{
			return (float)d;
		}
		return float.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out float r) ? r : fallback;
	}

	private static object ParseValue(string s, ref int i)
	{
		SkipWs(s, ref i);
		if (i >= s.Length)
		{
			throw new FormatException("unexpected end of input");
		}
		char c = s[i];
		switch (c)
		{
		case '{':
			return ParseObject(s, ref i);
		case '[':
			return ParseArray(s, ref i);
		case '"':
			return ParseString(s, ref i);
		case 't':
			Expect(s, ref i, "true");
			return true;
		case 'f':
			Expect(s, ref i, "false");
			return false;
		case 'n':
			Expect(s, ref i, "null");
			return null;
		default:
			return ParseNumber(s, ref i);
		}
	}

	private static Dictionary<string, object> ParseObject(string s, ref int i)
	{
		Dictionary<string, object> d = new Dictionary<string, object>();
		i++;
		SkipWs(s, ref i);
		if (i < s.Length && s[i] == '}')
		{
			i++;
			return d;
		}
		while (true)
		{
			SkipWs(s, ref i);
			string key = ParseString(s, ref i);
			SkipWs(s, ref i);
			if (i >= s.Length || s[i] != ':')
			{
				throw new FormatException("expected ':' at " + i);
			}
			i++;
			d[key] = ParseValue(s, ref i);
			SkipWs(s, ref i);
			if (i >= s.Length)
			{
				throw new FormatException("unterminated object");
			}
			if (s[i] == ',')
			{
				i++;
				continue;
			}
			if (s[i] == '}')
			{
				i++;
				return d;
			}
			throw new FormatException("expected ',' or '}' at " + i);
		}
	}

	private static List<object> ParseArray(string s, ref int i)
	{
		List<object> list = new List<object>();
		i++;
		SkipWs(s, ref i);
		if (i < s.Length && s[i] == ']')
		{
			i++;
			return list;
		}
		while (true)
		{
			list.Add(ParseValue(s, ref i));
			SkipWs(s, ref i);
			if (i >= s.Length)
			{
				throw new FormatException("unterminated array");
			}
			if (s[i] == ',')
			{
				i++;
				continue;
			}
			if (s[i] == ']')
			{
				i++;
				return list;
			}
			throw new FormatException("expected ',' or ']' at " + i);
		}
	}

	private static string ParseString(string s, ref int i)
	{
		if (s[i] != '"')
		{
			throw new FormatException("expected '\"' at " + i);
		}
		i++;
		StringBuilder sb = new StringBuilder();
		while (i < s.Length)
		{
			char c = s[i++];
			if (c == '"')
			{
				return sb.ToString();
			}
			if (c != '\\')
			{
				sb.Append(c);
				continue;
			}
			if (i >= s.Length)
			{
				break;
			}
			char e = s[i++];
			switch (e)
			{
			case '"': sb.Append('"'); break;
			case '\\': sb.Append('\\'); break;
			case '/': sb.Append('/'); break;
			case 'b': sb.Append('\b'); break;
			case 'f': sb.Append('\f'); break;
			case 'n': sb.Append('\n'); break;
			case 'r': sb.Append('\r'); break;
			case 't': sb.Append('\t'); break;
			case 'u':
				if (i + 4 > s.Length)
				{
					throw new FormatException("bad \\u escape");
				}
				sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
				i += 4;
				break;
			default:
				throw new FormatException("bad escape \\" + e);
			}
		}
		throw new FormatException("unterminated string");
	}

	private static object ParseNumber(string s, ref int i)
	{
		int start = i;
		bool fractional = false;
		while (i < s.Length)
		{
			char c = s[i];
			if (c == '-' || c == '+' || (c >= '0' && c <= '9'))
			{
				i++;
				continue;
			}
			if (c == '.' || c == 'e' || c == 'E')
			{
				fractional = true;
				i++;
				continue;
			}
			break;
		}
		string token = s.Substring(start, i - start);
		if (token.Length == 0)
		{
			throw new FormatException("expected a value at " + start);
		}
		if (!fractional && long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
		{
			return l;
		}
		return double.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture);
	}

	private static void Expect(string s, ref int i, string word)
	{
		if (i + word.Length > s.Length || string.CompareOrdinal(s, i, word, 0, word.Length) != 0)
		{
			throw new FormatException("expected '" + word + "' at " + i);
		}
		i += word.Length;
	}

	private static void SkipWs(string s, ref int i)
	{
		while (i < s.Length)
		{
			char c = s[i];
			if (c != ' ' && c != '\t' && c != '\r' && c != '\n')
			{
				break;
			}
			i++;
		}
	}

	private static void Write(StringBuilder sb, object value)
	{
		if (value == null)
		{
			sb.Append("null");
			return;
		}
		if (value is string s)
		{
			WriteString(sb, s);
			return;
		}
		if (value is bool b)
		{
			sb.Append(b ? "true" : "false");
			return;
		}
		if (value is int || value is long || value is short || value is byte || value is uint || value is ulong)
		{
			sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
			return;
		}
		if (value is float f)
		{
			sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
			return;
		}
		if (value is double d)
		{
			sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
			return;
		}
		if (value is IDictionary dict)
		{
			sb.Append('{');
			bool first = true;
			foreach (DictionaryEntry entry in dict)
			{
				if (!first)
				{
					sb.Append(',');
				}
				first = false;
				WriteString(sb, Convert.ToString(entry.Key, CultureInfo.InvariantCulture));
				sb.Append(':');
				Write(sb, entry.Value);
			}
			sb.Append('}');
			return;
		}
		if (value is IEnumerable list)
		{
			sb.Append('[');
			bool first = true;
			foreach (object item in list)
			{
				if (!first)
				{
					sb.Append(',');
				}
				first = false;
				Write(sb, item);
			}
			sb.Append(']');
			return;
		}
		WriteString(sb, Convert.ToString(value, CultureInfo.InvariantCulture));
	}

	private static void WriteString(StringBuilder sb, string s)
	{
		sb.Append('"');
		foreach (char c in s)
		{
			switch (c)
			{
			case '"': sb.Append("\\\""); break;
			case '\\': sb.Append("\\\\"); break;
			case '\b': sb.Append("\\b"); break;
			case '\f': sb.Append("\\f"); break;
			case '\n': sb.Append("\\n"); break;
			case '\r': sb.Append("\\r"); break;
			case '\t': sb.Append("\\t"); break;
			default:
				if (c < ' ')
				{
					sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
				}
				else
				{
					sb.Append(c);
				}
				break;
			}
		}
		sb.Append('"');
	}
}
