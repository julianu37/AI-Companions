using System;
using System.Reflection;
using UnityEngine;

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
			TopUpStamina();
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[DevBridge] pump error: " + ex.Message);
		}
	}

	/// <summary>
	/// Dev-only: keeps the player's stamina full (the player's request for test sessions, 02/11/2026 - god mode clamps a
	/// killing blow to 1 HP but says nothing about stamina, so cutting a forest for the build tests runs dry).
	/// Turned on by <c>world.setup</c> (<c>stamina</c>, default on) and by <c>player.stamina</c>. The bridge is off
	/// unless <c>[Advanced] DevBridge</c> is on, so this never reaches a player.
	/// </summary>
	internal static bool InfiniteStamina;

	private static float s_nextTopUp;
	private static FieldInfo s_staminaField;
	private static bool s_staminaLogged;

	private static void TopUpStamina()
	{
		if (!InfiniteStamina)
		{
			return;
		}
		Player player = Player.m_localPlayer;
		if (player == null)
		{
			return;
		}
		float now = Time.time;
		if (now < s_nextTopUp)
		{
			return;
		}
		s_nextTopUp = now + 0.25f;
		FillStamina(player);
	}

	/// <summary>Dev-only: fills the player's stamina right now (world.setup / player.stamina call it when turning the
	/// option on; the pump then keeps it full).</summary>
	internal static void FillStamina(Player player)
	{
		if (player == null)
		{
			return;
		}
		float max = player.GetMaxStamina();
		if (player.GetStamina() >= max)
		{
			return;
		}
		// 1.0 has GetStamina/Humanoid.UseStamina but no setter: the bar is a private float, so it is written through
		// reflection. FindStaminaField picks the field whose value IS GetStamina() - not a name guess - and the search
		// is retried on later frames while nothing was found (a world load can change what is reachable).
		if (s_staminaField == null)
		{
			s_staminaField = FindStaminaField(player);
		}
		if (s_staminaField == null)
		{
			if (!s_staminaLogged)
			{
				s_staminaLogged = true;
				CompanionsPlugin.Log?.LogWarning("[DevBridge] stamina infinita: nenhum campo de stamina com o valor de GetStamina() - a opcao nao faz efeito");
			}
			return;
		}
		if (!s_staminaLogged)
		{
			s_staminaLogged = true;
			CompanionsPlugin.Log?.LogInfo("[DevBridge] stamina infinita: " + s_staminaField.DeclaringType?.Name + "." + s_staminaField.Name);
		}
		s_staminaField.SetValue(player, max);
	}

	/// <summary>The float that holds the player's stamina. Valheim 1.0 keeps it in a private field somewhere in the
	/// hierarchy (Player/Humanoid/Character) and only exposes <c>GetStamina()</c>, so the field is picked by <b>value</b>:
	/// the one equal to <c>GetStamina()</c> right now. Names with max/regen/rate/drain are never candidates.</summary>
	private static FieldInfo FindStaminaField(Player player)
	{
		float current = player.GetStamina();
		FieldInfo best = null;
		float bestDelta = float.MaxValue;
		for (Type type = player.GetType(); type != null && type != typeof(object); type = type.BaseType)
		{
			foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (field.FieldType != typeof(float))
				{
					continue;
				}
				string name = field.Name;
				if (name.IndexOf("stamina", StringComparison.OrdinalIgnoreCase) < 0)
				{
					continue;
				}
				if (name.IndexOf("max", StringComparison.OrdinalIgnoreCase) >= 0
					|| name.IndexOf("regen", StringComparison.OrdinalIgnoreCase) >= 0
					|| name.IndexOf("rate", StringComparison.OrdinalIgnoreCase) >= 0
					|| name.IndexOf("drain", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					continue;
				}
				try
				{
					float delta = Mathf.Abs((float)field.GetValue(player) - current);
					if (delta < bestDelta)
					{
						bestDelta = delta;
						best = field;
					}
				}
				catch
				{
					// a field that can't be read is not the one
				}
			}
		}
		return bestDelta <= 1f ? best : null;
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
