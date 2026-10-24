using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Companions;

/// <summary>
/// Fase 3.20: the <b>"Testar conexão"</b> of the AI panel's LLM section - one click, one probe, no polling.
///
/// <para>It runs on a thread pool task (the game never waits): first <c>GET {BaseUrl}/models</c>, which is what every
/// OpenAI-compatible server implements and answers with the list of models it can serve; when the server has no such
/// endpoint (404/405) a <b>minimal</b> <c>POST {BaseUrl}/chat/completions</c> is tried instead - one short message,
/// <c>max_tokens</c> 1 - so "it answers" can still be told apart from "there is nothing there". The answer comes back
/// through a thread-safe queue that <see cref="Tick"/> drains on the main thread, which is why the result is announced
/// on the HUD/chat even if the player closed the panel in the meantime.</para>
///
/// <para>Rules taken from <see cref="LlmClient"/> on purpose: the same <c>Proxy = null</c> (localhost must never go
/// through a system proxy), its own timeout per request, <c>KeepAlive = false</c>, and <b>never</b> the API key in a
/// log line - the key only travels in the <c>Authorization</c> header. The URL written to the log has its query
/// stripped (<see cref="LlmClient.DescribeEndpoint"/>), because a key can be part of a URL.</para>
/// </summary>
internal static class LlmConnectionTest
{
	/// <summary>One probe's outcome, built off the main thread and announced on it.</summary>
	private sealed class TestResult
	{
		internal bool Ok;

		/// <summary>How many models the server listed (0 when the answer came from the chat endpoint).</summary>
		internal int Models;

		/// <summary>Is the configured model in the list? (Only meaningful when <see cref="Models"/> &gt; 0.)</summary>
		internal bool ModelListed;

		internal string Model;

		/// <summary>True when the success came from the minimal chat request instead of the model list.</summary>
		internal bool ViaChat;

		/// <summary>Why it failed, as the server described it (status/message). Never contains the key.</summary>
		internal string Error;
	}

	/// <summary>What one HTTP request came back with.</summary>
	private sealed class HttpAnswer
	{
		internal bool Ok;

		internal int Status;

		internal string Body;

		internal string Error;
	}

	/// <summary>The "id" of each model in the /models body.</summary>
	private static readonly Regex ModelIdRegex = new Regex("\"id\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled);

	private static readonly ConcurrentQueue<TestResult> _finished = new ConcurrentQueue<TestResult>();

	private static int _running;

	/// <summary>Is a probe on its way? (The button refuses a second click while one is running.)</summary>
	internal static bool Running => Volatile.Read(ref _running) != 0;

	/// <summary>
	/// Starts one probe. Returns false when another one is still running (the caller says so), so a click never piles
	/// up requests. Every value is read here, on the main thread.
	/// </summary>
	internal static bool Start(string baseUrl, string model, string apiKey, int timeoutSeconds)
	{
		if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
		{
			return false;
		}
		string url = BaseOf(baseUrl);
		string name = model != null ? model.Trim() : "";
		string key = apiKey != null ? apiKey.Trim() : "";
		int timeoutMs = Mathf.Clamp(timeoutSeconds, 5, 30) * 1000;
		CompanionsPlugin.Log?.LogInfo($"[IA] Conexao: testando {LlmClient.DescribeEndpoint(url)} (modelo \"{name}\", "
			+ $"chave {(key.Length > 0 ? "informada" : "vazia")}) - em segundo plano");
		Task.Run(delegate
		{
			TestResult result;
			try
			{
				result = Probe(url, name, key, timeoutMs);
			}
			catch (Exception ex)
			{
				result = new TestResult
				{
					Ok = false,
					Model = name,
					Error = ex.Message
				};
			}
			_finished.Enqueue(result);
		});
		return true;
	}

	/// <summary>Called every frame from the plugin's Update: announces what a probe found. Never blocks, never allocates
	/// when idle.</summary>
	internal static void Tick()
	{
		while (_finished.TryDequeue(out TestResult result))
		{
			Interlocked.Exchange(ref _running, 0);
			string line = Describe(result);
			CompanionsPlugin.Log?.LogInfo($"[IA] Conexao: {Text(result)}");
			CompanionChatAI.Announce(line);
		}
	}

	/// <summary>The base of the API, without the chat endpoint a player may have pasted in the box.</summary>
	private static string BaseOf(string baseUrl)
	{
		string url = baseUrl != null ? baseUrl.Trim() : "";
		url = url.TrimEnd('/');
		if (url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
		{
			url = url.Substring(0, url.Length - "/chat/completions".Length).TrimEnd('/');
		}
		return url;
	}

	private static TestResult Probe(string baseUrl, string model, string apiKey, int timeoutMs)
	{
		HttpAnswer models = Send(baseUrl + "/models", "GET", null, apiKey, timeoutMs);
		if (models.Ok)
		{
			List<string> ids = ModelIds(models.Body);
			return new TestResult
			{
				Ok = true,
				Models = ids.Count,
				ModelListed = Contains(ids, model),
				Model = model
			};
		}
		// no /models on this server (or it refused): a minimal chat request tells "it answers" from "there is nothing
		// there". Only tried when the model list was NOT a real answer from the server - a timeout is not retried (the
		// player would wait twice as long for the same silence).
		if (models.Status != 0)
		{
			HttpAnswer chat = Send(baseUrl + "/chat/completions", "POST", ChatBody(model), apiKey, timeoutMs);
			if (chat.Ok)
			{
				return new TestResult
				{
					Ok = true,
					ViaChat = true,
					Model = model
				};
			}
			if (chat.Status != 0)
			{
				return new TestResult
				{
					Ok = false,
					Model = model,
					Error = $"/models: {models.Error}; /chat/completions: {chat.Error}"
				};
			}
			return new TestResult
			{
				Ok = false,
				Model = model,
				Error = chat.Error
			};
		}
		return new TestResult
		{
			Ok = false,
			Model = model,
			Error = models.Error
		};
	}

	/// <summary>One request, with the same precautions as <see cref="LlmClient"/> uses for the orders.</summary>
	private static HttpAnswer Send(string url, string method, string body, string apiKey, int timeoutMs)
	{
		HttpAnswer answer = new HttpAnswer();
		try
		{
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
			request.Method = method;
			request.Accept = "application/json";
			request.Timeout = timeoutMs;
			request.ReadWriteTimeout = timeoutMs;
			request.KeepAlive = false;
			request.Proxy = null;   // localhost: never through a system proxy
			if (!string.IsNullOrEmpty(apiKey))
			{
				request.Headers["Authorization"] = "Bearer " + apiKey;
			}
			if (body != null)
			{
				byte[] payload = new UTF8Encoding(false).GetBytes(body);
				request.ContentType = "application/json; charset=utf-8";
				request.ContentLength = payload.Length;
				using (Stream stream = request.GetRequestStream())
				{
					stream.Write(payload, 0, payload.Length);
				}
			}
			using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
			using (Stream stream = response.GetResponseStream())
			using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
			{
				answer.Status = (int)response.StatusCode;
				answer.Body = reader.ReadToEnd();
				answer.Ok = true;
			}
		}
		catch (WebException wex)
		{
			answer.Ok = false;
			answer.Status = StatusOf(wex);
			string detail = ShortBody(wex);
			if (wex.Status == WebExceptionStatus.ProtocolError && answer.Status > 0)
			{
				answer.Error = detail.Length > 0 ? $"HTTP {answer.Status}: {detail}" : $"HTTP {answer.Status}";
			}
			else
			{
				answer.Error = detail.Length > 0 ? $"{wex.Message} ({detail})" : wex.Message;
			}
		}
		catch (Exception ex)
		{
			answer.Ok = false;
			answer.Error = ex.Message;
		}
		return answer;
	}

	/// <summary>The first ~90 characters of the error body, on one line: it is usually the server saying why it refused
	/// ("model not found", "invalid api key"). Never the request, so it can't carry the key.</summary>
	private static string ShortBody(WebException wex)
	{
		try
		{
			if (!(wex.Response is HttpWebResponse response))
			{
				return "";
			}
			using (Stream stream = response.GetResponseStream())
			{
				if (stream == null)
				{
					return "";
				}
				using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
				{
					string body = reader.ReadToEnd();
					if (string.IsNullOrEmpty(body))
					{
						return "";
					}
					body = body.Replace('\n', ' ').Replace('\r', ' ').Trim();
					return body.Length > 90 ? body.Substring(0, 90) + "..." : body;
				}
			}
		}
		catch
		{
			return "";
		}
	}

	private static int StatusOf(WebException wex)	{
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

	/// <summary>The ids the /models body lists (empty when it isn't the shape we know).</summary>
	private static List<string> ModelIds(string body)
	{
		List<string> ids = new List<string>();
		if (string.IsNullOrEmpty(body))
		{
			return ids;
		}
		foreach (Match match in ModelIdRegex.Matches(body))
		{
			string id = match.Groups[1].Value;
			if (!string.IsNullOrEmpty(id))
			{
				ids.Add(id);
			}
		}
		return ids;
	}

	private static bool Contains(List<string> ids, string model)
	{
		if (string.IsNullOrEmpty(model))
		{
			return false;
		}
		for (int i = 0; i < ids.Count; i++)
		{
			if (string.Equals(ids[i], model, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	private static string ChatBody(string model)
	{
		return "{\"model\":\"" + LlmClient.EscapeJson(model) + "\",\"messages\":[{\"role\":\"user\",\"content\":\"ping\"}],"
			+ "\"max_tokens\":1,\"stream\":false}";
	}

	/// <summary>The line the player reads (localized).</summary>
	private static string Describe(TestResult result)
	{
		if (result == null)
		{
			return "";
		}
		if (!result.Ok)
		{
			return ModLocalization.LocFmt("hc_ai_llm_test_fail", string.IsNullOrEmpty(result.Error) ? "?" : result.Error);
		}
		if (result.ViaChat)
		{
			return ModLocalization.Loc("hc_ai_llm_test_ok_chat");
		}
		if (string.IsNullOrEmpty(result.Model))
		{
			return ModLocalization.LocFmt("hc_ai_llm_test_ok_count", result.Models);
		}
		if (result.ModelListed)
		{
			return ModLocalization.LocFmt("hc_ai_llm_test_ok_models", result.Models);
		}
		return ModLocalization.LocFmt("hc_ai_llm_test_ok_nomodel", result.Models,
			string.IsNullOrEmpty(result.Model) ? "?" : result.Model);
	}

	/// <summary>The same outcome for the log: ASCII, with the count and the status, and never the key.</summary>
	private static string Text(TestResult result)
	{
		if (result == null)
		{
			return "?";
		}
		if (!result.Ok)
		{
			return "FALHOU - " + (result.Error ?? "?");
		}
		if (result.ViaChat)
		{
			return "OK - o servidor respondeu ao pedido minimo (sem /models)";
		}
		return $"OK - {result.Models} modelo(s); \"{result.Model}\" "
			+ (result.ModelListed ? "esta na lista" : "NAO esta na lista");
	}
}
