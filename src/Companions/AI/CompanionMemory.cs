using System;
using System.Collections.Generic;
using System.Text;

namespace Companions;

/// <summary>
/// Fase 3.5: the companion's short memory of the conversation. The last [IA] Memoria exchanges (one message from the
/// player + one from the companion = one exchange) are kept PER COMPANION and pasted into the next request as a short
/// text block, so the companion follows the thread ("como você está?" after "bom dia").
///
/// Choices worth knowing:
/// - The memory goes into the SYSTEM PROMPT as text, not as extra "user"/"assistant" messages. The JSON contract
///   stays the last thing the model reads (and the only thing it is asked to continue), and the model is never fed
///   its own past JSON back, which small local models tend to copy or paraphrase instead of answering.
/// - It is in memory only (nothing is written to the ZDO/save), capped on both sides: at most [IA] Memoria exchanges
///   per companion and at most MaxConversations companions, so the prompt can never grow without limit. It is
///   forgotten when the companion dies or is dismissed (see CompanionSetup).
/// - Every method here is called from the main thread (chat handling and the companion's death/destroy), never from
///   a frame loop, so there is no locking and no per-frame allocation.
/// </summary>
internal static class CompanionMemory
{
	/// <summary>Bucket used when no companion is nearby: the player is still talking to the team.</summary>
	internal const string PlayerKey = "player";

	/// <summary>How many companions are remembered at once; the oldest one is dropped when a new one shows up.</summary>
	private const int MaxConversations = 8;

	/// <summary>Longest single message kept, so one long message can't eat the whole prompt.</summary>
	private const int MaxTurnChars = 240;

	private const int MaxExchanges = 12;

	private sealed class Turn
	{
		internal bool FromPlayer;

		internal string Text;
	}

	private static readonly Dictionary<string, List<Turn>> s_turns = new Dictionary<string, List<Turn>>(StringComparer.Ordinal);

	/// <summary>Conversations, oldest first - only used to drop the least recently used one.</summary>
	private static readonly List<string> s_recent = new List<string>();

	/// <summary>[IA] Memoria: how many exchanges to keep (0 = memory off).</summary>
	internal static int Exchanges
	{
		get
		{
			int value = ModConfig.AiMemory != null ? ModConfig.AiMemory.Value : 4;
			return Math.Max(0, Math.Min(MaxExchanges, value));
		}
	}

	/// <summary>Adds one message ("Jogador" or the companion) to a companion's memory. Does nothing when memory is off.</summary>
	internal static void Remember(string key, bool fromPlayer, string text)
	{
		int exchanges = Exchanges;
		if (exchanges <= 0 || string.IsNullOrEmpty(key))
		{
			return;
		}
		string clean = Squash(text);
		if (clean.Length == 0)
		{
			return;
		}
		if (!s_turns.TryGetValue(key, out List<Turn> turns))
		{
			MakeRoom();
			turns = new List<Turn>(exchanges * 2);
			s_turns[key] = turns;
		}
		turns.Add(new Turn
		{
			FromPlayer = fromPlayer,
			Text = clean
		});
		// the newest exchange wins: the oldest messages fall off the front
		int limit = exchanges * 2;
		while (turns.Count > limit)
		{
			turns.RemoveAt(0);
		}
		Touch(key);
	}

	/// <summary>How many messages this companion remembers right now (for the log).</summary>
	internal static int Count(string key)
	{
		if (string.IsNullOrEmpty(key) || !s_turns.TryGetValue(key, out List<Turn> turns))
		{
			return 0;
		}
		return turns.Count;
	}

	/// <summary>
	/// Appends the recent conversation to the system prompt being built. Writes nothing when there is no memory or
	/// nothing was said yet, so a fresh companion gets exactly the prompt of Fase 3.6.
	/// </summary>
	internal static void AppendTo(StringBuilder builder, string key, string companionName)
	{
		if (builder == null || string.IsNullOrEmpty(key) || Exchanges <= 0)
		{
			return;
		}
		if (!s_turns.TryGetValue(key, out List<Turn> turns) || turns.Count == 0)
		{
			return;
		}
		string who = string.IsNullOrEmpty(companionName) ? ModLocalization.Loc("hc_ai_default_name") : companionName;
		builder.Append("\nConversa recente com o jogador (do mais antigo para o mais novo; use para lembrar o contexto):\n");
		for (int i = 0; i < turns.Count; i++)
		{
			if (turns[i].FromPlayer)
			{
				builder.Append("Jogador: ");
			}
			else
			{
				builder.Append(who).Append(": ");
			}
			builder.Append(turns[i].Text).Append('\n');
		}
	}

	/// <summary>Forgets one companion's conversation (death, dismissal, despawn).</summary>
	internal static void Clear(string key)
	{
		if (string.IsNullOrEmpty(key) || key == PlayerKey)
		{
			return;
		}
		s_turns.Remove(key);
		s_recent.Remove(key);
	}

	/// <summary>Forgets everything (world change / plugin shutdown).</summary>
	internal static void ClearAll()
	{
		s_turns.Clear();
		s_recent.Clear();
	}

	private static void MakeRoom()
	{
		while (s_recent.Count >= MaxConversations && s_recent.Count > 0)
		{
			string oldest = s_recent[0];
			s_recent.RemoveAt(0);
			s_turns.Remove(oldest);
		}
	}

	private static void Touch(string key)
	{
		s_recent.Remove(key);
		s_recent.Add(key);
	}

	/// <summary>One line, no line breaks, no leading/trailing blanks, capped.</summary>
	private static string Squash(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return "";
		}
		StringBuilder builder = new StringBuilder(Math.Min(text.Length, MaxTurnChars));
		bool lastWasSpace = false;
		for (int i = 0; i < text.Length && builder.Length < MaxTurnChars; i++)
		{
			char c = text[i];
			bool isSpace = c == '\n' || c == '\r' || c == '\t' || c == ' ';
			if (isSpace)
			{
				if (!lastWasSpace && builder.Length > 0)
				{
					builder.Append(' ');
					lastWasSpace = true;
				}
				continue;
			}
			builder.Append(c);
			lastWasSpace = false;
		}
		return builder.ToString().TrimEnd(' ');
	}
}
