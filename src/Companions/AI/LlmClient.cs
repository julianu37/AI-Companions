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

	internal string Error;

	internal string UserText;

	internal string Content;

	internal LlmIntent Intent;

	internal int ElapsedMs;
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

	private static float _busySince;

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
	/// Sends one order to the model. Returns immediately: the POST happens on a thread pool task.
	/// Called on the main thread, so every config value is read here.
	/// </summary>
	internal static void Request(string userText, string systemPrompt)
	{
		if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
		{
			CompanionsPlugin.Log?.LogInfo("[IA] A request is already in flight - ignoring this message");
			return;
		}
		_busySince = Time.realtimeSinceStartup;

		string url = Endpoint;
		string model = ModConfig.AiModel != null ? ModConfig.AiModel.Value : "";
		string apiKey = ModConfig.AiApiKey != null ? ModConfig.AiApiKey.Value : "";
		float temperature = ModConfig.AiTemperature != null ? ModConfig.AiTemperature.Value : 0.1f;
		int timeoutSeconds = ModConfig.AiTimeoutSeconds != null ? ModConfig.AiTimeoutSeconds.Value : 30;
		int timeoutMs = Mathf.Max(1000, timeoutSeconds * 1000);

		string body = BuildRequestBody(model, systemPrompt, userText, temperature);
		CompanionsPlugin.Log?.LogInfo($"[IA] Request -> {url} (model \"{model}\", timeout {timeoutSeconds}s) :: {userText}");
		// Fase 3.3b: how big the prompt really is. The model pays for every character it has to read before it can
		// answer, so this line is what tells whether a slow answer is a big prompt or a slow model.
		CompanionsPlugin.Log?.LogInfo($"[IA] Prompt: instrucoes {systemPrompt.Length} car. (~{EstimateTokens(systemPrompt.Length)} tokens) + mensagem {userText.Length} car. "
			+ $"= {systemPrompt.Length + userText.Length} car. (~{EstimateTokens(systemPrompt.Length + userText.Length)} tokens); corpo JSON {body.Length} car.");

		Task.Run(delegate
		{
			LlmResult result;
			try
			{
				result = Post(url, apiKey, body, timeoutMs, userText);
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
			_finished.Enqueue(result);
		});
	}

	/// <summary>Called from Update(). Hands back one finished call, or false when there is nothing to do.</summary>
	internal static bool TryDequeue(out LlmResult result)
	{
		return _finished.TryDequeue(out result);
	}

	/// <summary>
	/// Called from Update() right after TryDequeue returns false: releases the in-flight slot. Normally the slot is
	/// released when the result is read, but a request that hangs past its timeout (a stalled socket) must not lock
	/// the feature out forever.
	/// </summary>
	internal static void Tick()
	{
		if (Busy && Time.realtimeSinceStartup - _busySince > WatchdogSeconds())
		{
			CompanionsPlugin.Log?.LogWarning("[IA] Request timed out without an answer - clearing the in-flight slot");
			Interlocked.Exchange(ref _busy, 0);
		}
	}

	internal static void MarkDone()
	{
		Interlocked.Exchange(ref _busy, 0);
	}

	private static float WatchdogSeconds()
	{
		int timeoutSeconds = ModConfig.AiTimeoutSeconds != null ? ModConfig.AiTimeoutSeconds.Value : 30;
		return timeoutSeconds + 10f;
	}

	private static LlmResult Post(string url, string apiKey, string body, int timeoutMs, string userText)
	{
		LlmResult result = new LlmResult
		{
			UserText = userText
		};
		long started = DateTime.UtcNow.Ticks;
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
			CompanionsPlugin.Log?.LogWarning("[IA] Request failed: " + result.Error);
		}
		catch (Exception ex)
		{
			result.Ok = false;
			result.Error = ex.Message;
			CompanionsPlugin.Log?.LogWarning("[IA] Request failed: " + ex);
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

	private static string BuildRequestBody(string model, string systemPrompt, string userText, float temperature)
	{
		string temperatureText = temperature.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
		StringBuilder builder = new StringBuilder(512 + userText.Length + systemPrompt.Length);
		builder.Append("{\"model\":\"").Append(EscapeJson(model))
			.Append("\",\"temperature\":").Append(temperatureText)
			.Append(",\"stream\":false,\"messages\":[{\"role\":\"system\",\"content\":\"")
			.Append(EscapeJson(systemPrompt))
			.Append("\"},{\"role\":\"user\",\"content\":\"")
			.Append(EscapeJson(userText))
			.Append("\"}]}");
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
