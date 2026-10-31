using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using UnityEngine;

namespace Companions.DevBridge;

/// <summary>
/// The commands the MCP server can run. Everything here executes on the Unity main thread (see
/// <see cref="BridgeServer.Pump"/>), which is the only safe place to touch the game. Replies are the wire format
/// built by <see cref="Ok"/> / <see cref="Fail"/>.
///
/// This is the in-DLL port of the standalone DevBridge plugin: the mod's types are called directly (same assembly),
/// so a rename is a compile error instead of a reflection miss.
/// </summary>
internal static class Commands
{
	private sealed class ZdoKey
	{
		public readonly string Name;
		public readonly int Hash;
		public readonly string Kind;
		public readonly object Default;

		public ZdoKey(string name, int hash, string kind, object defaultValue = null)
		{
			Name = name;
			Hash = hash;
			Kind = kind;
			Default = defaultValue;
		}
	}

	// The companion keys worth reading by hand while developing. The hash is the mod's own field, so a renamed key
	// is a compile error here instead of silently reading the wrong number.
	private static readonly ZdoKey[] ZdoKeys =
	{
		new ZdoKey("Name", CompanionSetup.NameHash, "string"),
		new ZdoKey("Owner", CompanionSetup.OwnerHash, "string"),
		new ZdoKey("CompanionId", CompanionSetup.MHR_CompanionIdHash, "string"),
		new ZdoKey("ActionMode", CompanionSetup.ActionModeHash, "int"),
		new ZdoKey("CombatStance", CompanionSetup.CombatStanceHash, "int"),
		new ZdoKey("AutoPickup", CompanionSetup.AutoPickupHash, "bool", true),
		new ZdoKey("Wander", CompanionSetup.WanderHash, "bool"),
		new ZdoKey("Follow", CompanionSetup.FollowHash, "bool", true),
		new ZdoKey("StayHome", CompanionSetup.StayHomeHash, "bool"),
		new ZdoKey("HomeRadius", CompanionSetup.HomeRadiusHash, "float"),
		new ZdoKey("OrderCap", CompanionSetup.OrderCapHash, "int"),
		new ZdoKey("OrderCapMode", CompanionSetup.OrderCapModeHash, "int"),
		new ZdoKey("OrderCapItem", CompanionSetup.OrderCapItemHash, "string"),
		new ZdoKey("TombstoneId", CompanionSetup.TombstoneIdHash, "long"),
		new ZdoKey("Stamina", CompanionSetup.StaminaHash, "float"),
		new ZdoKey("Recovering", MHR_Recover.RecoveringHash, "bool"),
		new ZdoKey("RecoverStance", MHR_Recover.RecoverStanceHash, "int"),
		new ZdoKey("RecoverPickup", MHR_Recover.RecoverPickupHash, "bool", true),
		new ZdoKey("Voice", CompanionProfile.VoiceHash, "int"),
		new ZdoKey("Gen", MHR_Roster.GenHash, "int"),
		new ZdoKey("FarmZones", FarmZoneSerializer.FarmZonesHash, "zones")
	};

	public static string Fail(string error)
	{
		return "{\"ok\":false,\"error\":" + Json.Quote(error) + "}";
	}

	public static string Ok(object data)
	{
		return "{\"ok\":true,\"data\":" + Json.Serialize(data) + "}";
	}

	public static string Execute(string cmd, Dictionary<string, object> args)
	{
		try
		{
			switch (cmd)
			{
			case "ping": return Ping();
			case "status": return Status();
			case "console": return ConsoleCommand(args);
			case "player.give": return PlayerGive(args);
			case "player.teleport": return PlayerTeleport(args);
			case "player.kill": return PlayerKill();
			case "player.inventory": return PlayerInventory(args);
			case "world.spawn": return WorldSpawn(args);
			case "world.time": return WorldTime(args);
			case "screenshot": return Screenshot(args);
			case "render.view": return RenderView(args);
			case "input.focus": return InputFocus();
			case "input.key": return InputKey(args, "tap");
			case "input.key_down": return InputKey(args, "down");
			case "input.key_up": return InputKey(args, "up");
			case "input.text": return InputText(args);
			case "input.click": return InputClick(args);
			case "input.mouse_down": return InputMouse(args, down: true);
			case "input.mouse_up": return InputMouse(args, down: false);
			case "input.mouse_move": return InputMouseMove(args);
			case "companions.list": return CompanionsList();
			case "companions.spawn": return CompanionsSpawn();
			case "companions.order": return CompanionsOrder(args);
			case "companions.teleport": return CompanionsTeleport(args);
			case "companions.give": return CompanionsGive(args);
			case "companions.zdo": return CompanionsZdo(args);
			case "companions.kill": return CompanionsKill(args);
			case "companions.tier": return CompanionsTier(args);
			case "companions.hire": return CompanionsHire();
			case "companions.resurrect": return CompanionsResurrect(args);
			case "companions.farm_zone": return CompanionsFarmZone(args);
			default: return Fail("unknown command: " + cmd);
			}
		}
		catch (Exception ex)
		{
			return Fail(ex.GetType().Name + ": " + ex.Message);
		}
	}

	private static string Ping()
	{
		return Ok(new Dictionary<string, object>
		{
			{ "bridge", "Companions.DevBridge" },
			{ "version", CompanionsPlugin.PluginVersion },
			{ "inGame", Player.m_localPlayer != null },
			{ "world", ZNet.instance != null ? ZNet.instance.GetWorldName() : null },
			{ "mod", CompanionsPlugin.PluginVersion }
		});
	}

	private static string Status()
	{
		Player p = Player.m_localPlayer;
		Dictionary<string, object> data = new Dictionary<string, object>
		{
			{ "inGame", p != null }
		};
		if (p == null)
		{
			return Ok(data);
		}
		Vector3 pos = p.transform.position;
		data["player"] = p.GetPlayerName();
		data["x"] = pos.x;
		data["y"] = pos.y;
		data["z"] = pos.z;
		data["hp"] = p.GetHealth();
		data["maxHp"] = p.GetMaxHealth();
		data["stamina"] = p.GetStamina();
		data["biome"] = WorldGenerator.instance != null ? WorldGenerator.instance.GetBiome(pos).ToString() : null;
		data["world"] = ZNet.instance != null ? ZNet.instance.GetWorldName() : null;
		data["day"] = DayNumber();
		data["dayFraction"] = EnvMan.instance != null ? EnvMan.instance.GetDayFraction() : 0f;
		data["time"] = ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : 0f;
		return Ok(data);
	}

	private static int DayNumber()
	{
		if (EnvMan.instance == null)
		{
			return 0;
		}
		MethodInfo m = typeof(EnvMan).GetMethod("GetCurrentDay", BindingFlags.Instance | BindingFlags.NonPublic);
		return m != null ? Convert.ToInt32(m.Invoke(EnvMan.instance, null)) : 0;
	}

	private static string ConsoleCommand(Dictionary<string, object> args)
	{
		string command = Json.Str(args, "command");
		if (string.IsNullOrEmpty(command))
		{
			return Fail("missing 'command'");
		}
		if (global::Console.instance == null)
		{
			return Fail("the console is not available");
		}
		global::Console.instance.TryRunCommand(command, silentFail: true);
		return Ok(new Dictionary<string, object> { { "command", command } });
	}

	private static string PlayerGive(Dictionary<string, object> args)
	{
		Player p = Player.m_localPlayer;
		if (p == null)
		{
			return Fail("not in game");
		}
		string item = Json.Str(args, "item");
		int amount = Math.Max(1, Json.Int(args, "amount", 1));
		GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(item) : null;
		if (prefab == null)
		{
			return Fail("unknown item: " + item);
		}
		int added = AddItems(p.GetInventory(), prefab, amount);
		return Ok(new Dictionary<string, object>
		{
			{ "item", item },
			{ "amount", amount },
			{ "added", added == amount },
			{ "addedAmount", added }
		});
	}

	/// <summary>Adds up to <paramref name="amount"/> by chunks of the item's max stack size (coins cap at 999 per
	/// stack). Returns how many were actually added.</summary>
	private static int AddItems(Inventory inv, GameObject prefab, int amount)
	{
		if (inv == null || prefab == null || amount <= 0)
		{
			return 0;
		}
		ItemDrop drop = prefab.GetComponent<ItemDrop>();
		int maxStack = drop != null && drop.m_itemData != null && drop.m_itemData.m_shared != null
			? Math.Max(1, drop.m_itemData.m_shared.m_maxStackSize)
			: amount;
		int remaining = amount;
		int added = 0;
		while (remaining > 0)
		{
			int chunk = Math.Min(remaining, maxStack);
			if (!inv.AddItem(prefab, chunk))
			{
				break;
			}
			added += chunk;
			remaining -= chunk;
		}
		return added;
	}

	private static string PlayerTeleport(Dictionary<string, object> args)
	{
		Player p = Player.m_localPlayer;
		if (p == null)
		{
			return Fail("not in game");
		}
		Vector3 pos = TargetPosition(args, p.transform.position);
		p.transform.position = pos;
		return Ok(new Dictionary<string, object> { { "x", pos.x }, { "y", pos.y }, { "z", pos.z } });
	}

	private static string PlayerKill()
	{
		Player p = Player.m_localPlayer;
		if (p == null)
		{
			return Fail("not in game");
		}
		if (p.IsDead())
		{
			return Ok(new Dictionary<string, object> { { "killed", false }, { "alreadyDead", true } });
		}
		if (p.InGodMode())
		{
			return Fail("the player is in god mode - run game_console 'god' first");
		}
		if (p.InGhostMode())
		{
			return Fail("the player is in ghost mode - leave it first");
		}
		if (p.IsDebugFlying())
		{
			return Fail("the player is in debug flight - leave it first");
		}
		if (p.IsTeleporting())
		{
			return Fail("the player is teleporting - try again");
		}
		float hp = p.GetHealth();
		HitData hit = new HitData();
		hit.m_damage.m_damage = Math.Max(hp + 1f, 1000000f);
		hit.m_point = p.transform.position;
		hit.m_dir = Vector3.zero;
		hit.m_hitType = HitData.HitType.Undefined;
		p.ApplyDamage(hit, true, false);
		float after = p.GetHealth();
		if (after > 0f)
		{
			return Fail("the player is still alive (" + after + " hp) - see the log");
		}
		return Ok(new Dictionary<string, object>
		{
			{ "killed", true },
			{ "hpBefore", hp },
			{ "damage", hit.m_damage.m_damage },
			{ "hpAfter", after }
		});
	}

	private static string PlayerInventory(Dictionary<string, object> args)
	{
		Player p = Player.m_localPlayer;
		if (p == null)
		{
			return Fail("not in game");
		}
		Inventory inv = p.GetInventory();
		if (inv == null)
		{
			return Fail("the player has no inventory");
		}
		string item = Json.Str(args, "item");
		bool setRequested = args != null && args.ContainsKey("set");
		if (setRequested && string.IsNullOrEmpty(item))
		{
			return Fail("'set' needs 'item'");
		}
		string internalName = null;
		if (!string.IsNullOrEmpty(item))
		{
			GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(item) : null;
			ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
			if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null)
			{
				return Fail("unknown item: " + item);
			}
			internalName = drop.m_itemData.m_shared.m_name;
			if (setRequested)
			{
				int want = Math.Max(0, Json.Int(args, "set", 0));
				int have = inv.CountItems(internalName);
				if (have > 0)
				{
					inv.RemoveItem(internalName, have);
				}
				if (want > 0)
				{
					AddItems(inv, prefab, want);
				}
			}
		}
		List<object> stacks = new List<object>();
		foreach (ItemDrop.ItemData data in inv.GetAllItems())
		{
			if (data == null || data.m_shared == null)
			{
				continue;
			}
			stacks.Add(new Dictionary<string, object>
			{
				{ "prefab", data.m_dropPrefab != null ? data.m_dropPrefab.name : null },
				{ "name", data.m_shared.m_name },
				{ "amount", data.m_stack },
				{ "quality", data.m_quality },
				{ "equipped", data.m_equipped },
				{ "grid", data.m_gridPos.x + "," + data.m_gridPos.y }
			});
		}
		return Ok(new Dictionary<string, object>
		{
			{ "item", item },
			{ "count", internalName != null ? inv.CountItems(internalName) : 0 },
			{ "stacks", stacks },
			{ "totalStacks", inv.NrOfItems() },
			{ "weight", inv.GetTotalWeight() },
			{ "maxWeight", p.GetMaxCarryWeight() }
		});
	}

	private static string WorldSpawn(Dictionary<string, object> args)
	{
		string name = Json.Str(args, "prefab");
		GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
		if (prefab == null)
		{
			return Fail("unknown prefab: " + name);
		}
		Vector3 origin = Player.m_localPlayer != null
			? Player.m_localPlayer.transform.position + Player.m_localPlayer.transform.forward * 3f
			: Vector3.zero;
		Vector3 pos = TargetPosition(args, origin);
		int count = Mathf.Clamp(Json.Int(args, "count", 1), 1, 20);
		for (int i = 0; i < count; i++)
		{
			UnityEngine.Object.Instantiate(prefab, pos + new Vector3(i * 1.5f, 0f, 0f), Quaternion.identity);
		}
		return Ok(new Dictionary<string, object> { { "prefab", name }, { "count", count } });
	}

	private static string WorldTime(Dictionary<string, object> args)
	{
		if (ZNet.instance == null || EnvMan.instance == null)
		{
			return Fail("the world isn't loaded");
		}
		if (!ZNet.instance.IsServer())
		{
			return Fail("world_time only works on the local (single-player) host");
		}
		long dayLength = EnvMan.instance.m_dayLengthSec;
		double before = ZNet.instance.GetTimeSeconds();
		double target = before;
		if (args != null && args.ContainsKey("skip"))
		{
			target = before + Json.Float(args, "skip", 0f);
			if (target < 0.0)
			{
				target = 0.0;
			}
		}
		else
		{
			int day = (int)Math.Floor(before / dayLength);
			double inDay = before - day * (double)dayLength;
			if (args != null && args.ContainsKey("day"))
			{
				day = Json.Int(args, "day", day);
			}
			if (args != null && args.ContainsKey("fraction"))
			{
				inDay = Mathf.Clamp01(Json.Float(args, "fraction", (float)(inDay / dayLength))) * (double)dayLength;
			}
			else if (args != null && args.ContainsKey("time"))
			{
				inDay = Mathf.Clamp(Json.Float(args, "time", (float)inDay), 0f, (float)(dayLength - 1));
			}
			target = day * (double)dayLength + inDay;
		}
		if (target < 0.0)
		{
			target = 0.0;
		}
		ZNet.instance.SetNetTime(target);
		double wrapped = target % dayLength;
		return Ok(new Dictionary<string, object>
		{
			{ "before", before },
			{ "time", target },
			{ "day", EnvMan.instance.GetDay(target) },
			{ "fraction", wrapped / dayLength },
			{ "dayLength", dayLength }
		});
	}

	private static string Screenshot(Dictionary<string, object> args)
	{
		string file = Json.Str(args, "path");
		if (string.IsNullOrEmpty(file))
		{
			string dir = Path.Combine(Paths.BepInExRootPath, "DevBridge");
			Directory.CreateDirectory(dir);
			file = Path.Combine(dir, "shot-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".png");
		}
		ScreenCapture.CaptureScreenshot(file, 1);
		return Ok(new Dictionary<string, object> { { "path", file } });
	}

	/// <summary>
	/// 1.9.4 (vision): renders the world from an INDEPENDENT off-screen camera - it never touches the player's view.
	/// (x, z) is the point to look at; yaw/pitch orbit the camera around it and dist is how far the camera sits. The
	/// camera is created disabled and rendered by hand into a temporary RenderTexture, so it cannot leak a frame to
	/// the screen. UI canvases are Screen Space - Overlay, so they are not in the picture (this is the world only).
	/// </summary>
	private static string RenderView(Dictionary<string, object> args)
	{
		if (ZNetScene.instance == null)
		{
			return Fail("the world isn't loaded");
		}
		float x = Json.Float(args, "x", float.NaN);
		float z = Json.Float(args, "z", float.NaN);
		if (float.IsNaN(x) || float.IsNaN(z))
		{
			return Fail("missing 'x'/'z' (the point to look at)");
		}
		Player p = Player.m_localPlayer;
		float y = Json.Float(args, "y", p != null ? p.transform.position.y : 0f);
		float yaw = Json.Float(args, "yaw", 0f);
		float pitch = Mathf.Clamp(Json.Float(args, "pitch", 15f), -85f, 85f);
		float dist = Mathf.Clamp(Json.Float(args, "dist", 8f), 1f, 200f);
		int size = Mathf.Clamp(Json.Int(args, "size", 768), 128, 2048);
		Vector3 target = new Vector3(x, y, z);
		float yawRad = yaw * Mathf.Deg2Rad;
		float pitchRad = pitch * Mathf.Deg2Rad;
		Vector3 offset = new Vector3(
			Mathf.Sin(yawRad) * Mathf.Cos(pitchRad),
			Mathf.Sin(pitchRad),
			Mathf.Cos(yawRad) * Mathf.Cos(pitchRad)) * dist;

		GameObject go = new GameObject("DevBridgeRenderCam");
		Camera cam = go.AddComponent<Camera>();
		cam.enabled = false;
		cam.clearFlags = CameraClearFlags.Skybox;
		cam.cullingMask = ~0;
		cam.nearClipPlane = 0.3f;
		cam.farClipPlane = 2000f;
		cam.fieldOfView = 60f;
		cam.transform.position = target + offset;
		cam.transform.LookAt(target);

		RenderTexture rt = RenderTexture.GetTemporary(size, size, 24);
		RenderTexture previous = RenderTexture.active;
		Texture2D tex = null;
		try
		{
			cam.targetTexture = rt;
			cam.Render();
			RenderTexture.active = rt;
			tex = new Texture2D(size, size, TextureFormat.RGB24, false);
			tex.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);
			tex.Apply();
			byte[] png = tex.EncodeToPNG();
			string dir = Path.Combine(Paths.BepInExRootPath, "DevBridge");
			Directory.CreateDirectory(dir);
			string file = Json.Str(args, "path");
			if (string.IsNullOrEmpty(file))
			{
				file = Path.Combine(dir, "render-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".png");
			}
			File.WriteAllBytes(file, png);
			return Ok(new Dictionary<string, object>
			{
				{ "path", file },
				{ "size", size },
				{ "bytes", png.Length },
				{ "target", new Dictionary<string, object> { { "x", target.x }, { "y", target.y }, { "z", target.z } } },
				{ "camera", new Dictionary<string, object>
					{
						{ "x", cam.transform.position.x },
						{ "y", cam.transform.position.y },
						{ "z", cam.transform.position.z },
						{ "yaw", yaw },
						{ "pitch", pitch },
						{ "dist", dist }
					}
				}
			});
		}
		finally
		{
			cam.targetTexture = null;
			RenderTexture.active = previous;
			RenderTexture.ReleaseTemporary(rt);
			if (tex != null)
			{
				UnityEngine.Object.Destroy(tex);
			}
			UnityEngine.Object.Destroy(go);
		}
	}

	private static string CompanionsList()
	{
		List<object> list = new List<object>();
		IReadOnlyList<CompanionSetup> all = CompanionSetup.AllCompanions;
		if (all != null)
		{
			for (int i = 0; i < all.Count; i++)
			{
				CompanionSetup cs = all[i];
				if (cs == null)
				{
					continue;
				}
				list.Add(DescribeCompanion(cs));
			}
		}
		return Ok(new Dictionary<string, object>
		{
			{ "count", list.Count },
			{ "companions", list },
			{ "tombstones", Tombstones() }
		});
	}

	private static List<object> Tombstones()
	{
		List<object> list = new List<object>();
		Player p = Player.m_localPlayer;
		if (p == null || ZNetScene.instance == null)
		{
			return list;
		}
		string owner = p.GetPlayerID().ToString();
		int nameHash = CompanionTombstoneResurrect.RespawnNameHash;
		int ownerHash = CompanionTombstoneResurrect.RespawnOwnerHash;
		int idHash = CompanionSetup.TombstoneIdHash;
		foreach (TombStone stone in UnityEngine.Object.FindObjectsByType<TombStone>(FindObjectsSortMode.None))
		{
			if (stone == null)
			{
				continue;
			}
			ZNetView nv = stone.GetComponent<ZNetView>();
			ZDO zdo = nv != null ? nv.GetZDO() : null;
			if (zdo == null || zdo.GetString(ownerHash, "") != owner)
			{
				continue;
			}
			Container container = stone.GetComponent<Container>();
			Inventory inventory = container != null ? container.GetInventory() : null;
			Vector3 pos = stone.transform.position;
			list.Add(new Dictionary<string, object>
			{
				{ "name", zdo.GetString(nameHash, "") },
				{ "items", inventory != null ? inventory.NrOfItems() : 0 },
				{ "id", zdo.GetLong(idHash, 0L) },
				{ "x", pos.x },
				{ "y", pos.y },
				{ "z", pos.z }
			});
		}
		return list;
	}

	private static Dictionary<string, object> DescribeCompanion(CompanionSetup cs)
	{
		Humanoid h = cs.GetComponent<Humanoid>();
		ZDO zdo = cs.GetComponent<ZNetView>()?.GetZDO();
		Vector3 pos = cs.transform.position;
		return new Dictionary<string, object>
		{
			{ "name", h != null ? h.m_name : cs.name },
			{ "prefab", cs.gameObject.name },
			{ "x", pos.x },
			{ "y", pos.y },
			{ "z", pos.z },
			{ "hp", h != null ? h.GetHealth() : 0f },
			{ "maxHp", h != null ? h.GetMaxHealth() : 0f },
			{ "dead", h != null && h.IsDead() },
			{ "distanceToPlayer", Player.m_localPlayer != null ? Vector3.Distance(Player.m_localPlayer.transform.position, pos) : 0f },
			{ "id", zdo != null ? zdo.GetString(CompanionSetup.MHR_CompanionIdHash, "") : null },
			{ "mode", zdo != null ? zdo.GetInt(CompanionSetup.ActionModeHash) : 0 },
			{ "stance", zdo != null ? zdo.GetInt(CompanionSetup.CombatStanceHash) : 0 },
			{ "autoPickup", zdo != null && zdo.GetBool(CompanionSetup.AutoPickupHash, defaultValue: true) },
			{ "recovering", zdo != null && zdo.GetBool(MHR_Recover.RecoveringHash) },
			{ "owner", zdo != null ? zdo.GetString(CompanionSetup.OwnerHash, "") : null }
		};
	}

	private static string CompanionsSpawn()
	{
		bool spawned = CompanionManager.SpawnFree(null, null);
		return spawned
			? Ok(new Dictionary<string, object> { { "spawned", true } })
			: Fail("SpawnFree returned false (see the BepInEx log: no player, no world or prefab missing)");
	}

	private static string CompanionsOrder(Dictionary<string, object> args)
	{
		string action = Json.Str(args, "action");
		if (string.IsNullOrEmpty(action))
		{
			return Fail("missing 'action'");
		}
		CompanionSetup target = FindCompanion(Json.Str(args, "name"));
		if (target == null)
		{
			string name = Json.Str(args, "name");
			return Fail("no companion found" + (string.IsNullOrEmpty(name) ? "" : " named \"" + name + "\""));
		}
		string a = action.ToLowerInvariant();
		if (a.StartsWith("stance_", StringComparison.Ordinal))
		{
			int stance = StanceOf(a);
			if (stance < 0)
			{
				return Fail("unknown stance: " + action);
			}
			target.SetCombatStance(stance);
			return Ok(new Dictionary<string, object> { { "action", action }, { "applied", "stance" }, { "stance", stance } });
		}
		if (a == "autopickup_on" || a == "autopickup_off")
		{
			bool on = a.EndsWith("on", StringComparison.Ordinal);
			target.SetAutoPickup(on);
			return Ok(new Dictionary<string, object> { { "action", action }, { "applied", "autopickup" }, { "value", on } });
		}
		int mode = ModeOf(a);
		if (mode < 0)
		{
			return Fail("unknown action: " + action);
		}
		bool applied = CompanionSetup.ApplyActionMode(target, mode, toggle: false);
		return Ok(new Dictionary<string, object>
		{
			{ "action", action },
			{ "mode", mode },
			{ "applied", applied }
		});
	}

	private static string CompanionsTeleport(Dictionary<string, object> args)
	{
		CompanionSetup cs = FindCompanion(Json.Str(args, "name"));
		if (cs == null)
		{
			return Fail("no companion found");
		}
		Vector3 fallback = Player.m_localPlayer != null
			? Player.m_localPlayer.transform.position + Player.m_localPlayer.transform.forward * 2f
			: cs.transform.position;
		Vector3 pos = TargetPosition(args, fallback);
		cs.transform.position = pos;
		return Ok(new Dictionary<string, object> { { "name", cs.name }, { "x", pos.x }, { "y", pos.y }, { "z", pos.z } });
	}

	private static string CompanionsGive(Dictionary<string, object> args)
	{
		CompanionSetup cs = FindCompanion(Json.Str(args, "name"));
		if (cs == null)
		{
			return Fail("no companion found");
		}
		Humanoid h = cs.GetComponent<Humanoid>();
		if (h == null || h.GetInventory() == null)
		{
			return Fail("the companion has no inventory");
		}
		string item = Json.Str(args, "item");
		int amount = Math.Max(1, Json.Int(args, "amount", 1));
		GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(item) : null;
		if (prefab == null)
		{
			return Fail("unknown item: " + item);
		}
		bool added = AddItems(h.GetInventory(), prefab, amount) == amount;
		return Ok(new Dictionary<string, object> { { "item", item }, { "amount", amount }, { "added", added } });
	}

	private static string CompanionsZdo(Dictionary<string, object> args)
	{
		CompanionSetup cs = FindCompanion(Json.Str(args, "name"));
		if (cs == null)
		{
			return Fail("no companion found");
		}
		ZDO zdo = cs.GetComponent<ZNetView>()?.GetZDO();
		if (zdo == null)
		{
			return Fail("the companion has no ZDO");
		}
		Dictionary<string, object> values = new Dictionary<string, object>();
		string rawZones = null;
		foreach (ZdoKey key in ZdoKeys)
		{
			switch (key.Kind)
			{
			case "string": values[key.Name] = zdo.GetString(key.Hash, ""); break;
			case "int": values[key.Name] = zdo.GetInt(key.Hash); break;
			case "long": values[key.Name] = zdo.GetLong(key.Hash, 0L); break;
			case "float": values[key.Name] = zdo.GetFloat(key.Hash); break;
			case "bool": values[key.Name] = zdo.GetBool(key.Hash, key.Default is bool b && b); break;
			case "zones":
				string zones = zdo.GetString(key.Hash, "");
				rawZones = zones;
				values[key.Name] = string.IsNullOrEmpty(zones) ? "<none>" : zones.Length + " chars";
				break;
			}
		}
		Humanoid h = cs.GetComponent<Humanoid>();
		return Ok(new Dictionary<string, object>
		{
			{ "name", h != null ? h.m_name : cs.name },
			{ "keys", values },
			{ "farmZones", ParseFarmZones(rawZones) }
		});
	}

	private static string CompanionsKill(Dictionary<string, object> args)
	{
		string name = Json.Str(args, "name");
		CompanionSetup cs = FindCompanion(name);
		if (cs == null)
		{
			return Fail("no companion found" + (string.IsNullOrEmpty(name) ? "" : " named \"" + name + "\""));
		}
		Character character = cs.GetComponent<Character>();
		if (character == null)
		{
			return Fail("the companion has no Character component");
		}
		if (character.IsDead())
		{
			return Ok(new Dictionary<string, object> { { "killed", false }, { "alreadyDead", true } });
		}
		float hp = character.GetHealth();
		float damage = Json.Float(args, "damage", 0f);
		if (damage <= 0f)
		{
			damage = Math.Max(hp + 1f, 1000000f);
		}
		HitData hit = new HitData();
		hit.m_damage.m_damage = damage;
		hit.m_point = cs.transform.position;
		hit.m_dir = Vector3.zero;
		hit.m_hitType = HitData.HitType.Undefined;
		character.ApplyDamage(hit, true, false);
		float after = character.GetHealth();
		if (after > 0f)
		{
			return Fail("\"" + character.m_name + "\" is still alive (" + after + " hp) - see the log");
		}
		return Ok(new Dictionary<string, object>
		{
			{ "name", character.m_name },
			{ "killed", true },
			{ "hpBefore", hp },
			{ "damage", damage },
			{ "hpAfter", after }
		});
	}

	private static string CompanionsTier(Dictionary<string, object> args)
	{
		string name = Json.Str(args, "name");
		string tier = Json.Str(args, "tier");
		if (string.IsNullOrEmpty(tier))
		{
			return Fail("missing 'tier'");
		}
		CompanionSetup cs = FindCompanion(name);
		if (cs == null)
		{
			return Fail("no companion found" + (string.IsNullOrEmpty(name) ? "" : " named \"" + name + "\""));
		}
		CompanionTierDef def = TierDef(tier);
		if (def == null)
		{
			return Fail("unknown tier: " + tier + " (companion, warrior, mage, fire, ice, support; or a prefab name like HC_DvergerFire)");
		}
		string before = cs.gameObject.name;
		Humanoid h = cs.GetComponent<Humanoid>();
		string beforeName = h != null ? h.m_name : cs.name;
		CompanionSetup after = CompanionManager.ChangeTier(cs, def);
		if (after == null)
		{
			return Fail("ChangeTier failed - see the [Tier] line in the log");
		}
		return Ok(new Dictionary<string, object>
		{
			{ "name", beforeName },
			{ "tier", def.DisplayName ?? tier },
			{ "prefabBefore", before },
			{ "prefabAfter", after.gameObject.name },
			{ "changed", !ReferenceEquals(after, cs) }
		});
	}

	private static string CompanionsHire()
	{
		Player p = Player.m_localPlayer;
		if (p == null)
		{
			return Fail("not in game");
		}
		Inventory inv = p.GetInventory();
		if (inv == null)
		{
			return Fail("the player has no inventory");
		}
		int price = CompanionTierData.Price;
		int before = inv.CountItems("$item_coins");
		CompanionAppearance appearance = CompanionAppearance.Default();
		bool hired = CompanionManager.HireWithCoins(appearance);
		int after = inv.CountItems("$item_coins");
		Dictionary<string, object> data = new Dictionary<string, object>
		{
			{ "hired", hired },
			{ "price", price },
			{ "coinsBefore", before },
			{ "coinsAfter", after }
		};
		if (!hired)
		{
			data["reason"] = before < price ? "not enough coins" : "the spawn failed (see the log)";
		}
		return Ok(data);
	}

	private static string CompanionsResurrect(Dictionary<string, object> args)
	{
		Player p = Player.m_localPlayer;
		if (p == null)
		{
			return Fail("not in game");
		}
		string owner = p.GetPlayerID().ToString();
		string want = Json.Str(args, "name");
		int nameHash = CompanionTombstoneResurrect.RespawnNameHash;
		int ownerHash = CompanionTombstoneResurrect.RespawnOwnerHash;
		int idHash = CompanionSetup.TombstoneIdHash;
		TombStone found = null;
		ZDO foundZdo = null;
		string foundName = "";
		foreach (TombStone stone in UnityEngine.Object.FindObjectsByType<TombStone>(FindObjectsSortMode.None))
		{
			if (stone == null)
			{
				continue;
			}
			ZNetView nv = stone.GetComponent<ZNetView>();
			ZDO zdo = nv != null ? nv.GetZDO() : null;
			if (zdo == null || zdo.GetString(ownerHash, "") != owner)
			{
				continue;
			}
			string n = zdo.GetString(nameHash, "");
			if (!string.IsNullOrEmpty(want) && n.IndexOf(want, StringComparison.OrdinalIgnoreCase) < 0)
			{
				continue;
			}
			found = stone;
			foundZdo = zdo;
			foundName = n;
			break;
		}
		if (found == null)
		{
			return Fail("no companion tombstone found" + (string.IsNullOrEmpty(want) ? " for this player" : " for \"" + want + "\""));
		}
		CompanionTombstoneResurrect comp = found.GetComponent<CompanionTombstoneResurrect>();
		if (comp == null)
		{
			return Fail("the tombstone has no CompanionTombstoneResurrect component");
		}
		Vector3 pos = found.transform.position;
		CompanionTombstoneResurrect.MHR_ResurrectCore(foundZdo, comp, false);
		CompanionSetup revived = FindLiveCompanion(foundName);
		Character character = revived != null ? revived.GetComponent<Character>() : null;
		if (character == null || character.IsDead() || character.GetHealth() <= 0f)
		{
			return Fail("the resurrection did not produce a live companion - see the [Resurrect] line in the log");
		}
		return Ok(new Dictionary<string, object>
		{
			{ "name", foundName },
			{ "resurrected", true },
			{ "hp", character.GetHealth() },
			{ "x", pos.x },
			{ "y", pos.y },
			{ "z", pos.z },
			{ "tombstoneId", foundZdo.GetLong(idHash, 0L) }
		});
	}

	private static string CompanionsFarmZone(Dictionary<string, object> args)
	{
		CompanionSetup cs = FindCompanion(Json.Str(args, "name"));
		if (cs == null)
		{
			return Fail("no companion found");
		}
		ZDO zdo = cs.GetComponent<ZNetView>()?.GetZDO();
		if (zdo == null)
		{
			return Fail("the companion has no ZDO");
		}
		int hash = FarmZoneSerializer.FarmZonesHash;
		string action = Json.Str(args, "action", "list");
		string raw = zdo.GetString(hash, "");
		if (action == "list")
		{
			return Ok(new Dictionary<string, object>
			{
				{ "action", action },
				{ "count", string.IsNullOrEmpty(raw) ? 0 : raw.Split('|').Length },
				{ "zones", ParseFarmZones(raw) }
			});
		}
		if (action == "clear")
		{
			zdo.Set(hash, "");
			return Ok(new Dictionary<string, object> { { "action", action }, { "count", 0 }, { "zones", new List<object>() } });
		}
		if (action != "add")
		{
			return Fail("unknown action: " + action + " (add, list, clear)");
		}
		Vector3 fallback = Player.m_localPlayer != null ? Player.m_localPlayer.transform.position : cs.transform.position;
		Vector3 center = TargetPosition(args, fallback);
		float half = Math.Max(0.5f, Json.Float(args, "halfSize", 4f));
		string crop = Json.Str(args, "crop", "");
		string zone = string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4},0",
			center.x, center.y, center.z, half, crop ?? "");
		zdo.Set(hash, raw.Length > 0 ? raw + "|" + zone : zone);
		string after = zdo.GetString(hash, "");
		return Ok(new Dictionary<string, object>
		{
			{ "action", action },
			{ "count", after.Split('|').Length },
			{ "zones", ParseFarmZones(after) }
		});
	}

	private static string InputFocus()
	{
		bool focused = InputSim.FocusGame();
		return Ok(new Dictionary<string, object>
		{
			{ "focused", focused },
			{ "window", InputSim.GameWindow().ToInt64() }
		});
	}

	private static string InputKey(Dictionary<string, object> args, string mode)
	{
		string key = Json.Str(args, "key");
		ushort vk = InputSim.VirtualKey(key);
		if (vk == 0)
		{
			return Fail("unknown key: " + key + " (use KeyCode names: Escape, F8, Return, W, Alpha1...)");
		}
		if (!InputSim.FocusGame())
		{
			return Fail("could not focus the game window - another window has the focus");
		}
		bool sent = mode == "down" ? InputSim.Down(vk) : mode == "up" ? InputSim.Up(vk) : InputSim.Tap(vk);
		return Ok(new Dictionary<string, object>
		{
			{ "key", key },
			{ "vk", (int)vk },
			{ "mode", mode },
			{ "sent", sent },
			{ "focused", InputSim.IsGameFocused() }
		});
	}

	private static string InputText(Dictionary<string, object> args)
	{
		string text = Json.Str(args, "text");
		if (string.IsNullOrEmpty(text))
		{
			return Fail("missing 'text'");
		}
		if (!InputSim.FocusGame())
		{
			return Fail("could not focus the game window - another window has the focus");
		}
		bool sent = InputSim.Text(text);
		return Ok(new Dictionary<string, object>
		{
			{ "text", text },
			{ "sent", sent },
			{ "focused", InputSim.IsGameFocused() }
		});
	}

	private static string InputClick(Dictionary<string, object> args)
	{
		int x = Json.Int(args, "x", -1);
		int y = Json.Int(args, "y", -1);
		if (x < 0 || y < 0)
		{
			return Fail("missing 'x'/'y' (game screen pixels)");
		}
		if (!InputSim.FocusGame())
		{
			return Fail("could not focus the game window - another window has the focus");
		}
		bool sent = InputSim.Click(x, y);
		return Ok(new Dictionary<string, object>
		{
			{ "x", x },
			{ "y", y },
			{ "sent", sent },
			{ "focused", InputSim.IsGameFocused() }
		});
	}

	private static string InputMouseMove(Dictionary<string, object> args)
	{
		int dx = Json.Int(args, "dx", 0);
		int dy = Json.Int(args, "dy", 0);
		if (dx == 0 && dy == 0)
		{
			return Fail("missing 'dx'/'dy' (mouse delta in pixels)");
		}
		InputSim.FocusGame();
		InputSim.MouseMove(dx, dy);
		return Ok(new Dictionary<string, object>
		{
			{ "dx", dx },
			{ "dy", dy },
			{ "focused", InputSim.IsGameFocused() }
		});
	}

	private static string InputMouse(Dictionary<string, object> args, bool down)
	{
		int x = Json.Int(args, "x", -1);
		int y = Json.Int(args, "y", -1);
		if (x < 0 || y < 0)
		{
			return Fail("missing 'x'/'y' (game screen pixels)");
		}
		if (!InputSim.FocusGame())
		{
			return Fail("could not focus the game window - another window has the focus");
		}
		bool sent = down ? InputSim.MouseDown(x, y) : InputSim.MouseUp(x, y);
		return Ok(new Dictionary<string, object>
		{
			{ "x", x },
			{ "y", y },
			{ "down", down },
			{ "sent", sent },
			{ "focused", InputSim.IsGameFocused() }
		});
	}

	private static CompanionTierDef TierDef(string wanted)
	{
		string needle = Normalize(wanted);
		switch (needle)
		{
		case "companion": return CompanionTierData.Companion;
		case "warrior": return CompanionTierData.DvergerWarrior;
		case "mage": return CompanionTierData.Dverger;
		case "fire":
		case "firemage": return CompanionTierData.DvergerFire;
		case "ice":
		case "icemage": return CompanionTierData.DvergerIce;
		case "support": return CompanionTierData.DvergerSupport;
		}
		CompanionTierDef[] all = CompanionTierData.All;
		if (all != null)
		{
			for (int i = 0; i < all.Length; i++)
			{
				CompanionTierDef def = all[i];
				if (def == null)
				{
					continue;
				}
				if (needle == Normalize(def.PrefabName) || needle == Normalize(def.DisplayName) ||
					needle == Normalize((def.PrefabName ?? "").Replace("HC_", "")))
				{
					return def;
				}
			}
		}
		return null;
	}

	private static string Normalize(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return "";
		}
		StringBuilder sb = new StringBuilder(value.Length);
		foreach (char ch in value)
		{
			if (char.IsLetterOrDigit(ch))
			{
				sb.Append(char.ToLowerInvariant(ch));
			}
		}
		return sb.ToString();
	}

	private static List<object> ParseFarmZones(string raw)
	{
		List<object> zones = new List<object>();
		if (string.IsNullOrEmpty(raw))
		{
			return zones;
		}
		foreach (string part in raw.Split('|'))
		{
			string[] fields = part.Split(',');
			if (fields.Length < 4)
			{
				continue;
			}
			if (!TryFloat(fields[0], out float x) || !TryFloat(fields[1], out float y) ||
				!TryFloat(fields[2], out float z) || !TryFloat(fields[3], out float half))
			{
				continue;
			}
			string crop = fields.Length > 4 ? fields[4] : "";
			float rotation = 0f;
			if (fields.Length > 5)
			{
				TryFloat(fields[5], out rotation);
			}
			zones.Add(new Dictionary<string, object>
			{
				{ "x", x },
				{ "y", y },
				{ "z", z },
				{ "halfSize", half },
				{ "crop", crop },
				{ "rotation", rotation }
			});
		}
		return zones;
	}

	private static bool TryFloat(string text, out float value)
	{
		return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
	}

	private static CompanionSetup FindCompanion(string name)
	{
		IReadOnlyList<CompanionSetup> all = CompanionSetup.AllCompanions;
		if (all == null || all.Count == 0)
		{
			return null;
		}
		if (string.IsNullOrEmpty(name))
		{
			for (int i = 0; i < all.Count; i++)
			{
				if (all[i] != null)
				{
					return all[i];
				}
			}
			return null;
		}
		for (int i = 0; i < all.Count; i++)
		{
			CompanionSetup cs = all[i];
			if (cs == null)
			{
				continue;
			}
			Humanoid h = cs.GetComponent<Humanoid>();
			string n = h != null ? h.m_name : cs.name;
			if (!string.IsNullOrEmpty(n) && n.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return cs;
			}
		}
		return null;
	}

	private static CompanionSetup FindLiveCompanion(string name)
	{
		IReadOnlyList<CompanionSetup> all = CompanionSetup.AllCompanions;
		if (all == null)
		{
			return null;
		}
		for (int i = 0; i < all.Count; i++)
		{
			CompanionSetup cs = all[i];
			if (cs == null)
			{
				continue;
			}
			Character character = cs.GetComponent<Character>();
			if (character != null && character.IsDead())
			{
				continue;
			}
			Humanoid h = cs.GetComponent<Humanoid>();
			string n = h != null ? h.m_name : cs.name;
			if (!string.IsNullOrEmpty(name) && (string.IsNullOrEmpty(n) || n.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0))
			{
				continue;
			}
			return cs;
		}
		return null;
	}

	private static int ModeOf(string action)
	{
		switch (action)
		{
		case "follow": return CompanionSetup.ModeFollow;
		case "wood": return CompanionSetup.ModeGatherWood;
		case "stone": return CompanionSetup.ModeGatherStone;
		case "ore": return CompanionSetup.ModeGatherOre;
		case "stay": return CompanionSetup.ModeStay;
		case "forage": return CompanionSetup.ModeForage;
		case "smelt": return CompanionSetup.ModeSmelt;
		case "hunt": return CompanionSetup.ModeHunt;
		case "farm": return CompanionSetup.ModeFarm;
		case "fish": return CompanionSetup.ModeFish;
		case "repair": return CompanionSetup.ModeRepairBuildings;
		case "restock": return CompanionSetup.ModeRestock;
		case "cook": return CompanionSetup.ModeCook;
		default: return -1;
		}
	}

	private static int StanceOf(string action)
	{
		switch (action)
		{
		case "stance_balanced": return CompanionSetup.StanceBalanced;
		case "stance_passive": return CompanionSetup.StancePassive;
		case "stance_ranged": return CompanionSetup.StanceRanged;
		case "stance_melee": return CompanionSetup.StanceMelee;
		default: return -1;
		}
	}

	private static Vector3 TargetPosition(Dictionary<string, object> args, Vector3 fallback)
	{
		bool hasX = args != null && args.ContainsKey("x");
		bool hasY = args != null && args.ContainsKey("y");
		bool hasZ = args != null && args.ContainsKey("z");
		if (!hasX && !hasY && !hasZ)
		{
			return fallback;
		}
		return new Vector3(Json.Float(args, "x", fallback.x), Json.Float(args, "y", fallback.y), Json.Float(args, "z", fallback.z));
	}
}
