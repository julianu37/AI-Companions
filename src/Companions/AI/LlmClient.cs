using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Companions;

/// <summary>
/// One finished (or failed) call to the model. Parsed on the background thread, applied on the main thread.
/// </summary>
internal sealed class LlmResult
{
	internal bool Ok;

	/// <summary>Fase 3.19: the request this answer belongs to (see <see cref="LlmClient.Request"/>). The chat applies an
	/// answer only when it is the answer to the message that is still pending - a late answer to a request the
	/// watchdog had already given up on used to be applied with the personality/speaker of the NEW message.</summary>
	internal int Id;

	internal string Error;

	internal string UserText;

	internal string Content;

	internal LlmIntent Intent;

	internal int ElapsedMs;

	/// <summary>HTTP status of the attempt that failed, or 0 when there was no HTTP answer at all (timeout, dead
	/// socket). Used to tell a server refusing response_format (400) from a real failure.</summary>
	internal int StatusCode;
}

/// <summary>
/// Fase 3.1: the HTTP client that talks to an OpenAI-compatible /chat/completions endpoint (LM Studio, Ollama,
/// llama.cpp, vLLM...). The request runs on a background task and the answer comes back through a thread-safe
/// queue that CompanionChatAI drains in Update() - the main thread is never blocked, never waits on .Result/.Wait().
/// </summary>
internal static class LlmClient
{
	private static readonly ConcurrentQueue<LlmResult> _finished = new ConcurrentQueue<LlmResult>();

	private static int _busy;

	/// <summary>Fase 3.19: the id of the request in flight (0 = none). Handed to <see cref="LlmResult.Id"/>, so an
	/// answer that arrives after its request was given up on can be told apart from the answer of a newer one.</summary>
	private static int _busyId;

	private static int _nextId;

	private static float _busySince;

	/// <summary>Fase 3.1 (saída estruturada): said once, on the first message that goes out with it.</summary>
	private static bool _loggedStructured;

	/// <summary>Fase 3.1 (saída estruturada): said once, when the server made the mod give up on it.</summary>
	private static bool _warnedNoStructured;

	/// <summary>True while a request is in flight (one at a time, so a chat burst can't flood the model).</summary>
	internal static bool Busy => Volatile.Read(ref _busy) != 0;

	/// <summary>The endpoint actually used, built from [IA] BaseUrl. Logged once so the log shows where orders go.</summary>
	internal static string Endpoint
	{
		get
		{
			string baseUrl = ModConfig.AiBaseUrl != null ? ModConfig.AiBaseUrl.Value : "";
			return BuildEndpoint(baseUrl);
		}
	}

	/// <summary>Fase 3.19 (auditoria): the endpoint for the log - scheme, host, port and path only. A key written in the
	/// URL (some remote server setups do that) must never reach the log.</summary>
	internal static string SafeEndpoint => DescribeEndpoint(Endpoint);

	/// <summary>Strips the query, the fragment and any user:password from a URL. Never throws.</summary>
	internal static string DescribeEndpoint(string url)
	{
		if (string.IsNullOrEmpty(url))
		{
			return "";
		}
		try
		{
			UriBuilder builder = new UriBuilder(url);
			builder.Query = "";
			builder.Fragment = "";
			builder.UserName = "";
			builder.Password = "";
			return builder.Uri.GetLeftPart(UriPartial.Path);
		}
		catch
		{
			int cut = url.IndexOfAny(new char[2] { '?', '#' });
			return cut < 0 ? url : url.Substring(0, cut);
		}
	}

	internal static string BuildEndpoint(string baseUrl)
	{
		if (string.IsNullOrEmpty(baseUrl))
		{
			baseUrl = "http://localhost:1234/v1";
		}
		string trimmed = baseUrl.Trim().TrimEnd('/');
		if (trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
		{
			return trimmed;
		}
		return trimmed + "/chat/completions";
	}

	/// <summary>
	/// Sends one order to the model. Returns immediately: the POST happens on a thread pool task. The returned id is
	/// what the caller has to look at when the answer comes back (<see cref="LlmResult.Id"/>); -1 means "not sent"
	/// (another request was in flight).
	/// Called on the main thread, so every config value is read here.
	/// </summary>
	internal static int Request(string userText, string systemPrompt)
	{
		if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
		{
			CompanionsPlugin.Log?.LogInfo("[IA] A request is already in flight - ignoring this message");
			return -1;
		}
		int id = Interlocked.Increment(ref _nextId);
		_busyId = id;
		_busySince = Time.realtimeSinceStartup;

		string url = Endpoint;
		string model = ModConfig.AiModel != null ? ModConfig.AiModel.Value : "";
		string apiKey = ModConfig.AiApiKey != null ? ModConfig.AiApiKey.Value : "";
		float temperature = ModConfig.AiTemperature != null ? ModConfig.AiTemperature.Value : 0.1f;
		int timeoutSeconds = ModConfig.AiTimeoutSeconds != null ? ModConfig.AiTimeoutSeconds.Value : 30;
		int timeoutMs = Mathf.Max(1000, timeoutSeconds * 1000);
		bool structured = ModConfig.AiStructuredOutput != null && ModConfig.AiStructuredOutput.Value;

		// Fase 3.1 (saída estruturada): the tries are built here, on the main thread, together with every other
		// config value - the background task only sends them. The first one is the request that normally goes out.
		LlmAttempt[] attempts = BuildAttempts(model, systemPrompt, userText, temperature, structured);
		string body = attempts[0].Body;
		CompanionsPlugin.Log?.LogInfo($"[IA] Request #{id} -> {DescribeEndpoint(url)} (model \"{model}\", timeout {timeoutSeconds}s) :: {userText}");
		if (structured)
		{
			LogStructuredOnce(body.Length);
		}
		// Fase 3.3b: how big the prompt really is. The model pays for every character it has to read before it can
		// answer, so this line is what tells whether a slow answer is a big prompt or a slow model.
		CompanionsPlugin.Log?.LogInfo($"[IA] Prompt: instrucoes {systemPrompt.Length} car. (~{EstimateTokens(systemPrompt.Length)} tokens) + mensagem {userText.Length} car. "
			+ $"= {systemPrompt.Length + userText.Length} car. (~{EstimateTokens(systemPrompt.Length + userText.Length)} tokens); corpo JSON {body.Length} car.");

		Task.Run(delegate
		{
			LlmResult result;
			try
			{
				result = Post(url, apiKey, attempts, timeoutMs, userText);
			}
			catch (Exception ex)
			{
				result = new LlmResult
				{
					Ok = false,
					UserText = userText,
					Error = ex.Message
				};
			}
			result.Id = id;
			_finished.Enqueue(result);
		});
		return id;
	}

	/// <summary>Called from Update(). Hands back one finished call, or false when there is nothing to do.</summary>
	internal static bool TryDequeue(out LlmResult result)
	{
		return _finished.TryDequeue(out result);
	}

	/// <summary>
	/// Called from Update() right after TryDequeue returns false: releases the in-flight slot. Normally the slot is
	/// released when the result is read, but a request that hangs past its timeout (a stalled socket) must not lock
	/// the feature out forever. Fase 3.19: the watchdog now hands an ERROR to the chat instead of only freeing the
	/// slot - the player was left without any answer at all - and the answer of the request it gave up on is dropped
	/// when (if) it finally arrives (see <see cref="LlmResult.Id"/>).
	/// </summary>
	internal static void Tick()
	{
		if (Busy && Time.realtimeSinceStartup - _busySince > WatchdogSeconds())
		{
			int id = _busyId;
			CompanionsPlugin.Log?.LogWarning($"[IA] Request #{id} timed out without an answer - the in-flight slot was freed and the chat was told");
			_finished.Enqueue(new LlmResult
			{
				Id = id,
				Ok = false,
				Error = ModLocalization.Loc("hc_ai_error_timeout")
			});
			Interlocked.Exchange(ref _busy, 0);
			_busyId = 0;
		}
	}

	/// <summary>Releases the in-flight slot, but only for the request that owns it (Fase 3.19: the answer of a request
	/// the watchdog already gave up on must not release the slot of the one that is running now).</summary>
	internal static void MarkDone(int id)
	{
		if (_busyId != 0 && _busyId != id)
		{
			return;
		}
		_busyId = 0;
		Interlocked.Exchange(ref _busy, 0);
	}

	private static float WatchdogSeconds()
	{
		int timeoutSeconds = ModConfig.AiTimeoutSeconds != null ? ModConfig.AiTimeoutSeconds.Value : 30;
		// Fase 3.1 (saída estruturada): one message may cost up to three attempts (json_schema -> json_object ->
		// plain), each with its own timeout, so the watchdog has to cover all of them. Otherwise it would free the
		// in-flight slot while a fallback was still running and the next message could start on top of it.
		int attempts = ModConfig.AiStructuredOutput != null && ModConfig.AiStructuredOutput.Value ? 3 : 1;
		return timeoutSeconds * attempts + 10f;
	}

	/// <summary>
	/// Sends the tries in order and stops at the first one the server accepts. Fase 3.1 (saída estruturada): the only
	/// reason to try again is the server REFUSING the requested response_format (HTTP 400, or an error naming
	/// response_format/json_schema) - a timeout or a dead socket is a real failure and repeating it would only make
	/// the player wait twice as long for the same error. The same body is never sent twice.
	/// </summary>
	private static LlmResult Post(string url, string apiKey, LlmAttempt[] attempts, int timeoutMs, string userText)
	{
		long started = DateTime.UtcNow.Ticks;
		LlmResult result = null;
		for (int i = 0; i < attempts.Length; i++)
		{
			LlmAttempt attempt = attempts[i];
			result = Send(url, apiKey, attempt.Body, timeoutMs, userText);
			if (result.Ok)
			{
				if (i > 0)
				{
					CompanionsPlugin.Log?.LogWarning($"[IA] Saida estruturada: o servidor recusou o formato pedido e a resposta veio \"{attempt.Label}\". "
						+ "Funciona igual, so sem a garantia de JSON valido - se isso se repetir em toda mensagem, desligue [IA] SaidaEstruturada.");
				}
				break;
			}
			if (i + 1 >= attempts.Length || !RejectedFormat(result))
			{
				break;
			}
			CompanionsPlugin.Log?.LogWarning($"[IA] Saida estruturada (\"{attempt.Label}\") recusada pelo servidor: {result.Error}");
			if (attempts[i + 1].ResponseFormat == null)
			{
				WarnNoStructuredOnce(result.Error);
			}
		}
		result.ElapsedMs = (int)((DateTime.UtcNow.Ticks - started) / TimeSpan.TicksPerMillisecond);
		if (result.Ok)
		{
			int outChars = result.Content != null ? result.Content.Length : 0;
			double seconds = Math.Max(0.001, result.ElapsedMs / 1000.0);
			CompanionsPlugin.Log?.LogInfo($"[IA] Tempo: {result.ElapsedMs} ms no total, resposta com {outChars} car. (~{EstimateTokens(outChars)} tokens, "
				+ $"~{EstimateTokens(outChars) / seconds:0.0} tokens/s contando prompt e resposta juntos).");
			if (result.ElapsedMs >= SlowRequestMs)
			{
				CompanionsPlugin.Log?.LogWarning($"[IA] Resposta lenta ({result.ElapsedMs} ms). Se isso se repetir, o gargalo e o modelo local, nao o mod: "
					+ "um modelo menor/mais rapido (ou com GPU ligada) responde bem mais depressa. Para comparar, veja a linha \"[IA] Prompt:\" acima "
					+ "e o tamanho da resposta.");
			}
		}
		return result;
	}

	/// <summary>One single try: one body, one timeout, one HTTP POST.</summary>
	private static LlmResult Send(string url, string apiKey, string body, int timeoutMs, string userText)
	{
		LlmResult result = new LlmResult
		{
			UserText = userText
		};
		try
		{
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
			request.Method = "POST";
			request.ContentType = "application/json; charset=utf-8";
			request.Accept = "application/json";
			request.Timeout = timeoutMs;
			request.ReadWriteTimeout = timeoutMs;
			request.KeepAlive = false;
			request.Proxy = null;      // localhost: never go through a system proxy
			if (!string.IsNullOrEmpty(apiKey))
			{
				request.Headers["Authorization"] = "Bearer " + apiKey;
			}

			byte[] payload = new UTF8Encoding(false).GetBytes(body);
			request.ContentLength = payload.Length;
			using (Stream requestStream = request.GetRequestStream())
			{
				requestStream.Write(payload, 0, payload.Length);
			}

			using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
			using (Stream stream = response.GetResponseStream())
			using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
			{
				string responseBody = reader.ReadToEnd();
				result.StatusCode = (int)response.StatusCode;
				result.Ok = true;
				result.Content = LlmIntent.ExtractContent(responseBody);
				result.Intent = LlmIntent.Parse(result.Content);
				CompanionsPlugin.Log?.LogInfo($"[IA] Response ({(int)response.StatusCode}) :: {responseBody}");
			}
		}
		catch (WebException wex)
		{
			result.Ok = false;
			result.Error = ReadWebException(wex);
			result.StatusCode = StatusCodeOf(wex);
			CompanionsPlugin.Log?.LogWarning("[IA] Request failed: " + result.Error);
		}
		catch (Exception ex)
		{
			result.Ok = false;
			result.Error = ex.Message;
			CompanionsPlugin.Log?.LogWarning("[IA] Request failed: " + ex);
		}
		return result;
	}

	/// <summary>
	/// True when the failure looks like the server refusing the requested response_format. HTTP 400 is the usual
	/// answer (LM Studio, llama.cpp); the message is also read because some servers say it in the body with another
	/// status. Anything else - timeout, dead socket, 500 - is a real failure and is reported as it is.
	/// </summary>
	private static bool RejectedFormat(LlmResult result)
	{
		if (result.StatusCode == 400)
		{
			return true;
		}
		string error = result.Error ?? "";
		return error.IndexOf("response_format", StringComparison.OrdinalIgnoreCase) >= 0
			|| error.IndexOf("json_schema", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static int StatusCodeOf(WebException wex)
	{
		try
		{
			if (wex.Response is HttpWebResponse response)
			{
				return (int)response.StatusCode;
			}
		}
		catch
		{
			// no status to read: treated as "no HTTP answer"
		}
		return 0;
	}

	/// <summary>Fase 3.1: said once, on the first message that goes out with structured output.</summary>
	private static void LogStructuredOnce(int bodyChars)
	{
		if (_loggedStructured)
		{
			return;
		}
		_loggedStructured = true;
		CompanionsPlugin.Log?.LogInfo($"[IA] Saida estruturada ligada ([IA] SaidaEstruturada): o pedido vai com response_format/json_schema "
			+ $"(\"companion_intent\", {CompanionChatAI.ValidActions.Length} acoes validas, corpo JSON de {bodyChars} car.). "
			+ "Se o servidor nao aceitar, o mod tenta {\"type\":\"json_object\"} e depois o formato antigo, avisando aqui.");
	}

	/// <summary>
	/// Fase 3.1: the mod is about to send the request the old way. Said once, because the server not supporting
	/// structured output is a property of the server and not of the message - the mod keeps working either way.
	/// </summary>
	private static void WarnNoStructuredOnce(string reason)
	{
		if (_warnedNoStructured)
		{
			return;
		}
		_warnedNoStructured = true;
		CompanionsPlugin.Log?.LogWarning("[IA] Saida estruturada: o servidor nao aceitou response_format/json_schema nem {\"type\":\"json_object\"} "
			+ $"({reason}). O mod segue no formato antigo (sem response_format) e nao vai tentar de novo nesta sessao - as ordens continuam "
			+ "funcionando, so sem a garantia de que o modelo responde um JSON valido. Para parar as tentativas de uma vez, desligue [IA] SaidaEstruturada.");
	}

	/// <summary>Above this, the log says out loud that the model is the bottleneck (Fase 3.3b).</summary>
	private const int SlowRequestMs = 5000;

	/// <summary>
	/// Rough token count of a text, only for the log (Portuguese runs about 3.5-4 characters per token with these
	/// tokenizers). Never used for anything but saying how big the prompt is.
	/// </summary>
	internal static int EstimateTokens(int chars)
	{
		return chars <= 0 ? 0 : (chars + 3) / 4;
	}

	private static string ReadWebException(WebException wex)
	{
		string text = wex.Message;
		try
		{
			if (wex.Response is HttpWebResponse response)
			{
				using (Stream stream = response.GetResponseStream())
				{
					if (stream != null)
					{
						using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
						{
							string body = reader.ReadToEnd();
							if (!string.IsNullOrEmpty(body))
							{
								text = $"HTTP {(int)response.StatusCode} {response.StatusDescription}: {body}";
							}
						}
					}
				}
			}
		}
		catch
		{
			// the status/body is a bonus, never a reason to fail
		}
		return text;
	}

	/// <summary>
	/// One try at the server (Fase 3.1, saída estruturada): the body to send and how the log names it. With
	/// structured output off there is a single try, the same request as before.
	/// </summary>
	private sealed class LlmAttempt
	{
		internal string Label;

		internal string ResponseFormat;

		internal string Body;
	}

	/// <summary>
	/// Fase 3.1 (saída estruturada): the chain of tries, one each, built on the main thread. The first one is what
	/// normally goes out; the others only happen when the server refuses the format (see Post).
	/// </summary>
	private static LlmAttempt[] BuildAttempts(string model, string systemPrompt, string userText, float temperature, bool structured)
	{
		LlmAttempt plain = new LlmAttempt
		{
			Label = "sem response_format",
			ResponseFormat = null,
			Body = BuildRequestBody(model, systemPrompt, userText, temperature, null)
		};
		if (!structured)
		{
			return new LlmAttempt[] { plain };
		}
		// json_schema is the one that guarantees a valid JSON (and the one that makes a small model answer in the
		// right shape); json_object is the simplest form the servers that don't implement the schema still accept
		// (LM Studio, Ollama); the old request is the last resort, so a server without structured output never
		// breaks the feature.
		return new LlmAttempt[]
		{
			new LlmAttempt
			{
				Label = "json_schema",
				ResponseFormat = JsonSchemaFormat,
				Body = BuildRequestBody(model, systemPrompt, userText, temperature, JsonSchemaFormat)
			},
			new LlmAttempt
			{
				Label = "json_object",
				ResponseFormat = JsonObjectFormat,
				Body = BuildRequestBody(model, systemPrompt, userText, temperature, JsonObjectFormat)
			},
			plain
		};
	}

	/// <summary>The simplest structured output, accepted by LM Studio and Ollama alike.</summary>
	private const string JsonObjectFormat = "{\"type\":\"json_object\"}";

	private static string _jsonSchemaFormat;

	/// <summary>
	/// Fase 3.1: the response_format that makes the server force a JSON object matching the mod's contract. Its
	/// "enum" of actions comes from <see cref="CompanionChatAI.ValidActions"/>, the same list the Contract
	/// describes, so the schema can't drift from what the mod understands. Built once and reused.
	/// </summary>
	private static string JsonSchemaFormat
	{
		get
		{
			if (_jsonSchemaFormat == null)
			{
				_jsonSchemaFormat = BuildJsonSchemaFormat();
			}
			return _jsonSchemaFormat;
		}
	}

	private static string BuildJsonSchemaFormat()
	{
		StringBuilder actions = new StringBuilder(256);
		string[] valid = CompanionChatAI.ValidActions;
		for (int i = 0; i < valid.Length; i++)
		{
			if (i > 0)
			{
				actions.Append(',');
			}
			actions.Append('"').Append(EscapeJson(valid[i])).Append('"');
		}
		return "{\"type\":\"json_schema\",\"json_schema\":{\"name\":\"companion_intent\",\"strict\":true,\"schema\":{"
			+ "\"type\":\"object\",\"properties\":{"
			+ "\"action\":{\"type\":\"string\",\"enum\":[" + actions + "]},"
			+ "\"reply\":{\"type\":\"string\"},"
			// Fase 3.11: the companion the message is addressed to - nullable like "target", and required (strict
			// mode wants every property in "required"), so a server that enforces the schema always sends the key
			// and the model has to decide between a name and null.
			+ "\"companion\":{\"type\":[\"string\",\"null\"]},"
			+ "\"target\":{\"type\":[\"string\",\"null\"]},"
			+ "\"resource\":{\"type\":[\"string\",\"null\"]},"
			+ "\"amount\":{\"type\":[\"integer\",\"null\"]}},"
			+ "\"required\":[\"action\",\"reply\",\"companion\",\"target\",\"resource\",\"amount\"],"
			+ "\"additionalProperties\":false}}}";
	}

	private static string BuildRequestBody(string model, string systemPrompt, string userText, float temperature, string responseFormat)
	{
		string temperatureText = temperature.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
		StringBuilder builder = new StringBuilder(512 + userText.Length + systemPrompt.Length);
		builder.Append("{\"model\":\"").Append(EscapeJson(model))
			.Append("\",\"temperature\":").Append(temperatureText)
			.Append(",\"stream\":false,\"messages\":[{\"role\":\"system\",\"content\":\"")
			.Append(EscapeJson(systemPrompt))
			.Append("\"},{\"role\":\"user\",\"content\":\"")
			.Append(EscapeJson(userText))
			.Append("\"}]");
		if (!string.IsNullOrEmpty(responseFormat))
		{
			builder.Append(",\"response_format\":").Append(responseFormat);
		}
		builder.Append('}');
		return builder.ToString();
	}

	internal static string EscapeJson(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return "";
		}
		StringBuilder builder = new StringBuilder(value.Length + 16);
		foreach (char c in value)
		{
			switch (c)
			{
			case '"':
				builder.Append("\\\"");
				break;
			case '\\':
				builder.Append("\\\\");
				break;
			case '\n':
				builder.Append("\\n");
				break;
			case '\r':
				builder.Append("\\r");
				break;
			case '\t':
				builder.Append("\\t");
				break;
			default:
				if (c < ' ')
				{
					builder.Append("\\u").Append(((int)c).ToString("x4"));
				}
				else
				{
					builder.Append(c);
				}
				break;
			}
		}
		return builder.ToString();
	}
}
