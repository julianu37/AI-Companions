using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Companions.DevBridge;

/// <summary>
/// Loopback TCP server of the DevBridge (development tool, <c>[Advanced] DevBridge</c>). A background thread accepts
/// clients and reads one JSON request per line; every request is queued and executed by <see cref="Pump"/> on the
/// Unity main thread (the only place where the game's API is safe), then the reply is written back on the client's
/// thread.
///
/// Protocol (one JSON object per line, both ways):
///   request : {"cmd":"companions.list","args":{...},"token":"..."}
///   reply   : {"ok":true,"data":{...}}  |  {"ok":false,"error":"..."}
///
/// See docs/mcp-devbridge.md and tools/mcp-devbridge/.
/// </summary>
internal sealed class BridgeServer
{
	private sealed class Job
	{
		public string Cmd;
		public Dictionary<string, object> Args;
		public string Result;
		public readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
	}

	private readonly int _port;
	private readonly string _token;
	private readonly ConcurrentQueue<Job> _jobs = new ConcurrentQueue<Job>();
	private TcpListener _listener;
	private Thread _thread;
	private volatile bool _running;

	public BridgeServer(int port, string token)
	{
		_port = port;
		_token = token ?? "";
	}

	public void Start()
	{
		// loopback only on purpose: this can run console commands, it must never be reachable from the network
		_listener = new TcpListener(IPAddress.Loopback, _port);
		_listener.Start();
		_running = true;
		_thread = new Thread(AcceptLoop)
		{
			IsBackground = true,
			Name = "CompanionsDevBridge"
		};
		_thread.Start();
	}

	public void Stop()
	{
		_running = false;
		try
		{
			_listener?.Stop();
		}
		catch
		{
			// shutting down
		}
	}

	/// <summary>Called from the plugin's Update: executes everything the clients asked for, on the main thread.</summary>
	public void Pump()
	{
		while (_jobs.TryDequeue(out Job job))
		{
			try
			{
				job.Result = Commands.Execute(job.Cmd, job.Args);
			}
			catch (Exception ex)
			{
				job.Result = Commands.Fail(ex.GetType().Name + ": " + ex.Message);
			}
			job.Done.Set();
		}
	}

	private void AcceptLoop()
	{
		while (_running)
		{
			TcpClient client;
			try
			{
				client = _listener.AcceptTcpClient();
			}
			catch
			{
				break;
			}
			ThreadPool.QueueUserWorkItem(delegate
			{
				Serve(client);
			});
		}
	}

	private void Serve(TcpClient client)
	{
		using (client)
		using (NetworkStream stream = client.GetStream())
		using (StreamReader reader = new StreamReader(stream, new UTF8Encoding(false)))
		using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true })
		{
			string line;
			while (_running && (line = reader.ReadLine()) != null)
			{
				if (line.Length == 0)
				{
					continue;
				}
				string reply;
				try
				{
					Dictionary<string, object> root = Json.Parse(line) as Dictionary<string, object>;
					if (root == null)
					{
						reply = Commands.Fail("the request must be a JSON object");
					}
					else if (_token.Length > 0 && Json.Str(root, "token", "") != _token)
					{
						reply = Commands.Fail("unauthorized");
					}
					else
					{
						root.TryGetValue("args", out object argsValue);
						Job job = new Job
						{
							Cmd = Json.Str(root, "cmd", ""),
							Args = Json.Obj(argsValue) ?? new Dictionary<string, object>()
						};
						_jobs.Enqueue(job);
						reply = job.Done.Wait(8000) ? job.Result : Commands.Fail("timeout waiting for the game thread");
					}
				}
				catch (Exception ex)
				{
					reply = Commands.Fail("bad request: " + ex.Message);
				}
				writer.WriteLine(reply);
			}
		}
	}
}
