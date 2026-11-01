using System;

namespace Companions.DevBridge;

/// <summary>
/// Lifecycle of the in-DLL DevBridge (development tool, <c>[Advanced] DevBridge</c>): starts the loopback TCP server,
/// drains its queue every frame on the main thread and stops it on unload. Off by default - it can run console
/// commands, so it is never started unless the player turns it on. See docs/mcp-devbridge.md.
/// </summary>
internal sealed class DevBridgeService
{
	private readonly BridgeServer _server;
	private readonly int _port;

	public DevBridgeService(int port, string token)
	{
		_port = port;
		_server = new BridgeServer(port, token);
	}

	/// <summary>Starts the loopback listener. Answers false when the port could not be opened (another instance of
	/// the game, a socket left behind, another app): the caller drops the service and the mod keeps running without
	/// the development tool.</summary>
	public bool Start()
	{
		try
		{
			_server.Start();
			CompanionsPlugin.Log?.LogInfo($"[DevBridge] listening on 127.0.0.1:{_port} (v{CompanionsPlugin.PluginVersion}) - development tool, [Advanced] DevBridge");
			return true;
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogError("[DevBridge] could not start on port " + _port + ": " + ex.Message);
			return false;
		}
	}

	/// <summary>Called from the plugin's Update: the game API runs here, on the main thread.</summary>
	public void Pump()
	{
		try
		{
			_server.Pump();
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[DevBridge] pump error: " + ex.Message);
		}
	}

	public void Stop()
	{
		try
		{
			_server.Stop();
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[DevBridge] stop error: " + ex.Message);
		}
	}
}
