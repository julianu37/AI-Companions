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
		case "player.look": return PlayerLook(args);
			case "player.kill": return PlayerKill();
			case "player.inventory": return PlayerInventory(args);
			case "world.spawn": return WorldSpawn(args);
			case "world.time": return WorldTime(args);
			case "world.aim": return WorldAim(args);
			case "world.nearest": return WorldNearest(args);
			case "world.prefabs": return WorldPrefabs(args);
		case "build.inspect": return BuildInspect(args);
		case "build.sites": return BuildSites(args);
		case "build.pieces": return BuildPieces(args);
		case "build.find": return BuildFind(args);
		case "build.check": return BuildCheck(args);
		case "build.snaps": return BuildSnaps(args);
		case "build.usable": return BuildUsable(args);
		case "build.hoe": return BuildHoe();
		case "terrain.check": return TerrainCheck(args);
		case "terrain.level": return TerrainLevel(args);
		case "terrain.ops": return TerrainOps();
		case "companions.build": return CompanionsBuild(args);
		case "companions.level": return CompanionsLevel(args);
		case "companions.sethome": return CompanionsSetHome(args);
			case "world.tombstones": return WorldTombstones(args);
			case "world.tombstone_take": return WorldTombstoneTake(args);
			case "world.tombstone_destroy": return WorldTombstoneDestroy(args);
			case "world.tombstone_diag": return WorldTombstoneDiag(args);
			case "world.setup": return WorldSetup(args);
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
			case "companions.dismiss": return CompanionsDismiss(args);
			case "companions.inventory": return CompanionInventory(args);
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
		data["yaw"] = p.transform.eulerAngles.y;
		data["god"] = p.InGodMode();
		data["fightLikePlayer"] = CompanionCombatAI.FightLikePlayer;
		Camera cam = ViewCamera();
		if (cam != null)
		{
			Vector3 forward = cam.transform.forward;
			data["pitch"] = -Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg;
			data["camYaw"] = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
		}
		return Ok(data);
	}

	/// <summary>The camera the player is looking through (the game camera, or the main camera as a fallback).</summary>
	private static Camera ViewCamera()
	{
		if (GameCamera.instance != null)
		{
			Camera cam = GameCamera.instance.GetComponentInChildren<Camera>();
			if (cam != null)
			{
				return cam;
			}
		}
		return Camera.main;
	}

	/// <summary>What the crosshair is on: the hit collider, its distance and whether it is a tree/creature.</summary>
	private static string WorldAim(Dictionary<string, object> args)
	{
		Player p = Player.m_localPlayer;
		if (p == null)
		{
			return Fail("no player in the world");
		}
		Camera cam = ViewCamera();
		if (cam == null)
		{
			return Fail("no camera");
		}
		float max = Mathf.Clamp(Json.Float(args, "max", 100f), 1f, 200f);
		Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
		if (!Physics.Raycast(ray, out RaycastHit hit, max, ~0, QueryTriggerInteraction.Ignore))
		{
			return Ok(new Dictionary<string, object> { { "hit", false }, { "max", max } });
		}
		GameObject go = hit.collider != null ? hit.collider.gameObject : null;
		TreeBase tree = go != null ? go.GetComponentInParent<TreeBase>() : null;
		Character creature = go != null ? go.GetComponentInParent<Character>() : null;
		return Ok(new Dictionary<string, object>
		{
			{ "hit", true },
			{ "name", go != null ? go.name : null },
			{ "distance", hit.distance },
			{ "x", hit.point.x },
			{ "y", hit.point.y },
			{ "z", hit.point.z },
			{ "tree", tree != null },
			{ "creature", creature != null ? creature.GetHoverName() : null }
		});
	}

	/// <summary>The nearest tree or creature around the player (a dev aid to aim at something real).</summary>
	private static string WorldNearest(Dictionary<string, object> args)
	{
		Player p = Player.m_localPlayer;
		if (p == null)
		{
			return Fail("no player in the world");
		}
		string kind = (Json.Str(args, "kind") ?? "tree").Trim().ToLowerInvariant();
		float max = Mathf.Clamp(Json.Float(args, "max", 100f), 1f, 300f);
		Vector3 pos = p.transform.position;
		Transform best = null;
		float bestDistance = float.MaxValue;
		if (kind == "tree")
		{
			foreach (TreeBase tree in UnityEngine.Object.FindObjectsByType<TreeBase>(UnityEngine.FindObjectsSortMode.None))
			{
				if (tree == null)
				{
					continue;
				}
				float d = Vector3.Distance(pos, tree.transform.position);
				if (d < bestDistance && d <= max)
				{
					bestDistance = d;
					best = tree.transform;
				}
			}
		}
		else
		{
			foreach (Character c in UnityEngine.Object.FindObjectsByType<Character>(UnityEngine.FindObjectsSortMode.None))
			{
				if (c == null || c.IsPlayer() || c.IsDead())
				{
					continue;
				}
				float d = Vector3.Distance(pos, c.transform.position);
				if (d < bestDistance && d <= max)
				{
					bestDistance = d;
					best = c.transform;
				}
			}
		}
		if (best == null)
		{
			return Ok(new Dictionary<string, object> { { "found", false }, { "kind", kind }, { "max", max } });
		}
		return Ok(new Dictionary<string, object>
		{
			{ "found", true },
			{ "kind", kind },
			{ "name", best.gameObject.name },
			{ "distance", bestDistance },
			{ "x", best.position.x },
			{ "y", best.position.y },
			{ "z", best.position.z }
		});
	}

	private static int DayNumber()
	{
		// EnvMan.GetDay() reads the time from ZNet (decompiled 1.0.20: "return GetDay(ZNet.instance.GetTimeSeconds());"),
		// so it needs a connection - the same guard the caller uses for "world"/"time".
		if (EnvMan.instance == null || ZNet.instance == null)
		{
			return 0;
		}
		// B14 (audit 1.9.4): GetCurrentDay() is private, but EnvMan.GetDay() is public and answers the same day number.
		return EnvMan.instance.GetDay();
	}

	/// <summary>The prefab names the world knows (a filtered list), so the agent can find the exact name for
	/// <c>spawn</c> instead of guessing (e.g. 'AxeBlackmetal' vs 'AxeBlackMetal').</summary>
	private static string WorldPrefabs(Dictionary<string, object> args)
	{
		if (ZNetScene.instance == null)
		{
			return Fail("the world isn't loaded");
		}
		string filter = (Json.Str(args, "filter") ?? string.Empty).Trim();
		int limit = Mathf.Clamp(Json.Int(args, "limit", 60), 1, 500);
		List<string> matches = new List<string>();
		foreach (string name in ZNetScene.instance.GetPrefabNames())
		{
			if (name == null)
			{
				continue;
			}
			if (filter.Length == 0 || name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				matches.Add(name);
			}
		}
		matches.Sort(StringComparer.OrdinalIgnoreCase);
		if (matches.Count > limit)
		{
			matches.RemoveRange(limit, matches.Count - limit);
		}
		return Ok(new Dictionary<string, object>
		{
			{ "filter", filter },
			{ "count", matches.Count },
			{ "names", matches }
		});
	}

	/// <summary>
	/// Fase E0 (construção autônoma): the <b>sonda</b> - what a buildable piece is, read from the live prefab. The
	/// flags of <c>Piece</c> are not published anywhere (they live in the asset bundle), so this is how the agent
	/// learns what the workbench really needs instead of guessing.
	/// </summary>
	private static string BuildInspect(Dictionary<string, object> args)
	{
		if (ZNetScene.instance == null)
		{
			return Fail("the world isn't loaded");
		}
		string prefab = (Json.Str(args, "prefab") ?? string.Empty).Trim();
		if (prefab.Length == 0)
		{
			return Fail("prefab is required (e.g. piece_workbench)");
		}
		Dictionary<string, object> data = BuildProbe.Describe(prefab);
		if (!(data.TryGetValue("found", out object found) && found is bool ok && ok))
		{
			return Fail(Convert.ToString(data["reason"]));
		}
		return Ok(data);
	}

	/// <summary>
	/// Fase E0: tests a ring of candidate spots around the player (or around x/y/z) against the piece's placement
	/// rules, without placing anything. Each point comes with a stable status name, so a test in the game already
	/// speaks the vocabulary of the feature.
	/// </summary>
	private static string BuildSites(Dictionary<string, object> args)
	{
		if (ZNetScene.instance == null)
		{
			return Fail("the world isn't loaded");
		}
		string prefab = (Json.Str(args, "prefab") ?? string.Empty).Trim();
		if (prefab.Length == 0)
		{
			return Fail("prefab is required (e.g. piece_workbench)");
		}
		Piece piece = BuildProbe.PieceOf(prefab);
		if (piece == null)
		{
			return Fail("no Piece component on '" + prefab + "'");
		}
		Player p = Player.m_localPlayer;
		Vector3 center = p != null ? p.transform.position : Vector3.zero;
		float x = Json.Float(args, "x", center.x);
		float y = Json.Float(args, "y", center.y);
		float z = Json.Float(args, "z", center.z);
		center = new Vector3(x, y, z);
		float radius = Mathf.Clamp(Json.Float(args, "radius", 4f), 0.5f, 40f);
		int rings = Mathf.Clamp(Json.Int(args, "rings", 2), 1, 8);
		int dirs = Mathf.Clamp(Json.Int(args, "dirs", 8), 1, 24);
		List<object> points = BuildProbe.Scan(piece, center, radius, rings, dirs);
		int valid = 0;
		foreach (object entry in points)
		{
			Dictionary<string, object> point = entry as Dictionary<string, object>;
			if (point != null && point["ok"] is bool ok && ok)
			{
				valid++;
			}
		}
		return Ok(new Dictionary<string, object>
		{
			{ "prefab", prefab },
			{ "name", piece.m_name },
			{ "center", new Dictionary<string, object> { { "x", center.x }, { "y", center.y }, { "z", center.z } } },
			{ "radius", radius },
			{ "rings", rings },
			{ "dirs", dirs },
			{ "total", points.Count },
			{ "valid", valid },
			{ "points", points }
		});
	}

	/// <summary>
	/// The pieces already standing near a point (default: the player), with what the verification asks: the prefab,
	/// the creator, the health, whether it has a station and a valid ZDO. It is how a test proves a placement (or a
	/// relog) from the outside, and how the job's "Task Verification" is checked.
	/// </summary>
	private static string BuildPieces(Dictionary<string, object> args)
	{
		if (ZNetScene.instance == null)
		{
			return Fail("the world isn't loaded");
		}
		Player player = Player.m_localPlayer;
		Vector3 center = player != null ? player.transform.position : Vector3.zero;
		float x = Json.Float(args, "x", center.x);
		float y = Json.Float(args, "y", center.y);
		float z = Json.Float(args, "z", center.z);
		center = new Vector3(x, y, z);
		float radius = Mathf.Clamp(Json.Float(args, "radius", 10f), 0.5f, 100f);
		string filter = (Json.Str(args, "filter") ?? string.Empty).Trim();
		long me = player != null ? player.GetPlayerID() : 0L;
		List<object> found = new List<object>();
		foreach (Piece piece in UnityEngine.Object.FindObjectsByType<Piece>(UnityEngine.FindObjectsSortMode.None))
		{
			if (piece == null || !piece.gameObject.scene.IsValid())
			{
				continue;
			}
			float distance = Vector3.Distance(piece.transform.position, center);
			if (distance > radius)
			{
				continue;
			}
			if (filter.Length > 0 && piece.gameObject.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0
				&& piece.m_name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
			{
				continue;
			}
			ZNetView nv = piece.GetComponent<ZNetView>();
			WearNTear wear = piece.GetComponent<WearNTear>();
			CraftingStation station = piece.GetComponentInChildren<CraftingStation>();
			found.Add(new Dictionary<string, object>
			{
				{ "prefab", piece.gameObject.name },
				{ "name", piece.m_name },
				{ "creator", piece.GetCreator() },
				{ "mine", me != 0L && piece.GetCreator() == me },
				{ "distance", distance },
				{ "x", piece.transform.position.x },
				{ "y", piece.transform.position.y },
				{ "z", piece.transform.position.z },
				{ "zdo", nv != null && nv.IsValid() && nv.GetZDO() != null },
				{ "health", wear != null ? wear.GetHealthPercentage() : -1f },
				{ "station", station != null ? station.m_name : null }
			});
		}
		found.Sort((a, b) => ((float)((Dictionary<string, object>)a)["distance"]).CompareTo(((Dictionary<string, object>)b)["distance"]));
		return Ok(new Dictionary<string, object>
		{
			{ "center", new Dictionary<string, object> { { "x", center.x }, { "y", center.y }, { "z", center.z } } },
			{ "radius", radius },
			{ "count", found.Count },
			{ "pieces", found }
		});
	}

	/// <summary>
	/// Fase E2: where the piece would go. It runs the real search (<see cref="BuildSite.Find"/>) from a hint - the
	/// aim of the player when no coordinates are given - and returns the chosen pose, the distance from the hint and
	/// how many candidates were refused for each reason. Nothing is placed.
	/// </summary>
	/// <summary>
	/// Trilha E (E6): would the game accept this <b>exact</b> pose? It is the check the plan runs for every step -
	/// unlike <c>build.sites</c>, the y is <b>not</b> snapped to the ground (a wall on a floor or a roof on a wall
	/// lives above the terrain).
	/// </summary>
	private static string BuildCheck(Dictionary<string, object> args)
	{
		if (ZNetScene.instance == null)
		{
			return Fail("the world isn't loaded");
		}
		string prefab = (Json.Str(args, "prefab") ?? string.Empty).Trim();
		Piece piece = BuildProbe.PieceOf(prefab);
		if (piece == null)
		{
			return Fail(prefab.Length == 0 ? "prefab is required" : "no Piece component on '" + prefab + "'");
		}
		Vector3 pos = new Vector3(Json.Float(args, "x", 0f), Json.Float(args, "y", 0f), Json.Float(args, "z", 0f));
		float yaw = Json.Float(args, "yaw", 0f);
		Dictionary<string, object> check = BuildSite.Check(piece, pos, Quaternion.Euler(0f, yaw, 0f));
		check["prefab"] = prefab;
		return Ok(check);
	}

	/// <summary>
	/// Trilha E (E6): the piece's <b>snap points</b> (<c>Piece.GetSnapPoints</c>, the children tagged
	/// <c>snappoint</c>), in the prefab's own space. They are how the game lines a roof onto a wall; the template
	/// offsets were derived from them (see <c>docs/re/building-notes.md</c>).
	/// </summary>
	private static string BuildSnaps(Dictionary<string, object> args)
	{
		string prefab = (Json.Str(args, "prefab") ?? string.Empty).Trim();
		Piece piece = BuildProbe.PieceOf(prefab);
		if (piece == null)
		{
			return Fail(prefab.Length == 0 ? "prefab is required" : "no Piece component on '" + prefab + "'");
		}
		List<Transform> points = new List<Transform>();
		piece.GetSnapPoints(points);
		List<object> list = new List<object>();
		foreach (Transform point in points)
		{
			if (point == null)
			{
				continue;
			}
			Vector3 local = piece.transform.InverseTransformPoint(point.position);
			list.Add(new Dictionary<string, object>
			{
				{ "name", point.name },
				{ "x", local.x },
				{ "y", local.y },
				{ "z", local.z }
			});
		}
		return Ok(new Dictionary<string, object> { { "prefab", prefab }, { "count", list.Count }, { "points", list } });
	}

	/// <summary>
	/// Trilha E (E6): can the workbench in range of a point be <b>used</b>? It is the game's own
	/// <c>CraftingStation.CheckUsable</c> - the roof rule (<c>m_craftRequireRoof</c>, 70% cover) that placing a piece
	/// does not ask for. It is how the shelter is proved to turn the bench usable.
	/// </summary>
	private static string BuildUsable(Dictionary<string, object> args)
	{
		Player player = Player.m_localPlayer;
		if (player == null)
		{
			return Fail("no player in the world");
		}
		Vector3 pos = TargetPosition(args, player.transform.position);
		// the CLOSEST bench, not the first one the game lists: with two benches in range the registry order would
		// answer for the wrong one (measured 01/11/2026: the shelter was built over a bench and the tool still read the
		// old, uncovered one 10 m away)
		CraftingStation station = null;
		float best = float.MaxValue;
		List<CraftingStation> stations = new List<CraftingStation>();
		CraftingStation.FindStationsInRange("$piece_workbench", pos, 40f, stations);
		foreach (CraftingStation candidate in stations)
		{
			if (candidate == null)
			{
				continue;
			}
			float distance = Vector3.Distance(candidate.transform.position, pos);
			if (distance <= candidate.GetStationBuildRange() && distance < best)
			{
				best = distance;
				station = candidate;
			}
		}
		if (station == null)
		{
			return Fail("no workbench in range of that point");
		}
		bool usable = station.CheckUsable(player, false);
		float cover = -1f;
		bool underRoof = false;
		try
		{
			if (station.m_roofCheckPoint != null)
			{
				Cover.GetCoverForPoint(station.m_roofCheckPoint.position, out cover, out underRoof);
			}
		}
		catch (Exception)
		{
			// a diagnostic of the cover must not turn a question into an error
		}
		return Ok(new Dictionary<string, object>
		{
			{ "station", station.m_name },
			{ "usable", usable },
			{ "cover", cover },
			{ "underRoof", underRoof },
			{ "x", station.transform.position.x },
			{ "y", station.transform.position.y },
			{ "z", station.transform.position.z }
		});
	}

	/// <summary>
	/// Trilha E (E10): how uneven is the ground over a square area? The same sampling the build order does before it
	/// decides to level (<see cref="BuildTerrain.FlatEnough"/>), with the median height it would level to.
	/// </summary>
	private static string TerrainCheck(Dictionary<string, object> args)
	{
		if (ZNetScene.instance == null)
		{
			return Fail("the world isn't loaded");
		}
		Vector3 pos = TargetPosition(args, Player.m_localPlayer != null ? Player.m_localPlayer.transform.position : Vector3.zero);
		float size = Mathf.Clamp(Json.Float(args, "size", 8f), 1f, 40f);
		float tolerance = Json.Float(args, "tolerance", BuildTerrain.DefaultTolerance);
		float min;
		float max;
		float target;
		bool flat = BuildTerrain.FlatEnough(pos, size, tolerance, out min, out max, out target);
		return Ok(new Dictionary<string, object>
		{
			{ "flat", flat },
			{ "min", min },
			{ "max", max },
			{ "variation", max - min },
			{ "target", target },
			{ "size", size },
			{ "tolerance", tolerance },
			{ "hoePiece", BuildTerrain.Available() },
			{ "levelRadius", BuildTerrain.Radius() },
			{ "x", pos.x },
			{ "y", pos.y },
			{ "z", pos.z }
		});
	}

	/// <summary>
	/// Trilha E (E10): every terrain operation the game registered (<c>ObjectDB.m_terrainOps</c>) - the ones a hard
	/// level would use. The hoe's menu only carries soft/smooth/raise ones, so this is how we know whether a
	/// <c>m_level</c> operation exists anywhere in the game.
	/// </summary>
	private static string TerrainOps()
	{
		List<object> list = new List<object>();
		if (ObjectDB.instance != null && ObjectDB.instance.m_terrainOps != null)
		{
			foreach (TerrainOp op in ObjectDB.instance.m_terrainOps)
			{
				if (op == null || op.m_settings == null)
				{
					continue;
				}
				list.Add(new Dictionary<string, object>
				{
					{ "prefab", op.gameObject.name },
					{ "level", op.m_settings.m_level },
					{ "levelRadius", op.m_settings.m_levelRadius },
					{ "square", op.m_settings.m_square },
					{ "raise", op.m_settings.m_raise },
					{ "raiseRadius", op.m_settings.m_raiseRadius },
					{ "raiseDelta", op.m_settings.m_raiseDelta },
					{ "raisePower", op.m_settings.m_raisePower },
					{ "smooth", op.m_settings.m_smooth },
					{ "smoothRadius", op.m_settings.m_smoothRadius },
					{ "paint", op.m_settings.m_paintCleared },
					{ "levelOffset", op.m_settings.m_levelOffset }
				});
			}
		}
		return Ok(new Dictionary<string, object> { { "count", list.Count }, { "ops", list } });
	}

	/// <summary>Trilha E (E10): one leveling pass over an area (the game's soft level), for iterating on the numbers.</summary>
	private static string TerrainLevel(Dictionary<string, object> args)
	{
		if (ZNetScene.instance == null)
		{
			return Fail("the world isn't loaded");
		}
		Vector3 pos = TargetPosition(args, Player.m_localPlayer != null ? Player.m_localPlayer.transform.position : Vector3.zero);
		float size = Mathf.Clamp(Json.Float(args, "size", 8f), 1f, 40f);
		float target = Json.Float(args, "target", pos.y);
		string error;
		float spacing = Json.Float(args, "spacing", 1f);
		int applied = BuildTerrain.Level(pos, size, target, spacing, out error);
		return Ok(new Dictionary<string, object>
		{
			{ "applied", applied },
			{ "error", error },
			{ "target", target },
			{ "size", size },
			{ "radius", BuildTerrain.Radius() },
			{ "soft", BuildTerrain.Soft() }
		});
	}

	/// <summary>Trilha E (E10): what the <b>hoe</b> can do</summary>
	private static string BuildHoe()
	{
		GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("Hoe") : null;
		ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
		if (drop == null)
		{
			return Fail("no Hoe prefab");
		}
		List<object> pieces = new List<object>();
		PieceTable table = drop.m_itemData != null && drop.m_itemData.m_shared != null ? drop.m_itemData.m_shared.m_buildPieces : null;
		if (table != null && table.m_pieces != null)
		{
			foreach (GameObject go in table.m_pieces)
			{
				if (go == null)
				{
					continue;
				}
				TerrainOp[] ops = go.GetComponentsInChildren<TerrainOp>(true);
				TerrainModifier[] modifiers = go.GetComponentsInChildren<TerrainModifier>(true);
				Dictionary<string, object> entry = new Dictionary<string, object>
				{
					{ "prefab", go.name },
					{ "name", go.GetComponent<Piece>() != null ? go.GetComponent<Piece>().m_name : null },
					{ "terrainOps", ops.Length },
					{ "modifiers", modifiers.Length }
				};
				List<object> opList = new List<object>();
				foreach (TerrainOp op in ops)
				{
					if (op == null || op.m_settings == null)
					{
						continue;
					}
					opList.Add(new Dictionary<string, object>
					{
						{ "level", op.m_settings.m_level },
						{ "levelRadius", op.m_settings.m_levelRadius },
						{ "square", op.m_settings.m_square },
						{ "raise", op.m_settings.m_raise },
						{ "raiseRadius", op.m_settings.m_raiseRadius },
						{ "smooth", op.m_settings.m_smooth },
						{ "smoothRadius", op.m_settings.m_smoothRadius },
						{ "paint", op.m_settings.m_paintCleared },
						{ "paintRadius", op.m_settings.m_paintRadius },
						{ "levelOffset", op.m_settings.m_levelOffset }
					});
				}
				entry["ops"] = opList;
				if (ops.Length > 0 && ops[0] != null && ops[0].m_settings != null)
				{
					entry["level"] = ops[0].m_settings.m_level;
					entry["levelRadius"] = ops[0].m_settings.m_levelRadius;
				}
				if (modifiers.Length > 0 && modifiers[0] != null)
				{
					entry["modifierRadius"] = modifiers[0].GetRadius();
				}
				pieces.Add(entry);
			}
		}
		string spawn = drop.m_itemData != null && drop.m_itemData.m_shared != null && drop.m_itemData.m_shared.m_spawnOnHitTerrain != null
			? drop.m_itemData.m_shared.m_spawnOnHitTerrain.name
			: null;
		return Ok(new Dictionary<string, object>
		{
			{ "spawnOnHitTerrain", spawn },
			{ "count", pieces.Count },
			{ "pieces", pieces }
		});
	}

	private static string BuildFind(Dictionary<string, object> args)
	{
		if (ZNetScene.instance == null)
		{
			return Fail("the world isn't loaded");
		}
		string prefab = (Json.Str(args, "prefab") ?? string.Empty).Trim();
		if (prefab.Length == 0)
		{
			return Fail("prefab is required (e.g. piece_workbench)");
		}
		Piece piece = BuildProbe.PieceOf(prefab);
		if (piece == null)
		{
			return Fail("no Piece component on '" + prefab + "'");
		}
		Player player = Player.m_localPlayer;
		Vector3 forward = player != null ? player.transform.forward : Vector3.forward;
		Vector3 hint;
		if (player != null && BuildFindCommand.Hint(player, out hint))
		{
			// the aim is the natural "here"
		}
		else if (player != null)
		{
			Vector3 flat = player.transform.position + new Vector3(forward.x, 0f, forward.z).normalized * 3f;
			float y = ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(flat) : flat.y;
			hint = new Vector3(flat.x, y, flat.z);
		}
		else
		{
			hint = new Vector3(Json.Float(args, "x", 0f), Json.Float(args, "y", 0f), Json.Float(args, "z", 0f));
		}
		if (args.ContainsKey("x") || args.ContainsKey("z"))
		{
			hint = new Vector3(Json.Float(args, "x", hint.x), Json.Float(args, "y", hint.y), Json.Float(args, "z", hint.z));
		}
		float radius = Mathf.Clamp(Json.Float(args, "radius", 8f), 1f, 40f);
		int rings = Mathf.Clamp(Json.Int(args, "rings", 3), 1, 8);
		int dirs = Mathf.Clamp(Json.Int(args, "dirs", 8), 4, 24);
		BuildSiteResult site = BuildSite.Find(piece, hint, forward, radius, rings, dirs);
		return Ok(new Dictionary<string, object>
		{
			{ "prefab", prefab },
			{ "ok", site.Ok },
			{ "status", site.Status },
			{ "hint", new Dictionary<string, object> { { "x", hint.x }, { "y", hint.y }, { "z", hint.z } } },
			{ "pos", new Dictionary<string, object> { { "x", site.Pos.x }, { "y", site.Pos.y }, { "z", site.Pos.z } } },
			{ "yaw", site.Rot.eulerAngles.y },
			{ "distance", site.Distance },
			{ "tried", site.Tried },
			{ "reasons", site.Reasons },
			{ "tally", site.Tally }
		});
	}

	/// <summary>
	/// Trilha E (E3): gives a companion a build order without the LLM, so the job (find, walk, pay, place, verify) can
	/// be tested on its own. The piece must be in <see cref="BuildCatalog"/>; the spot is the player's aim, or 3 m
	/// ahead when nothing is under the crosshair.
	/// </summary>
	/// <summary>Trilha E (E10): the leveling job without the LLM - the companion works the ground point by point.</summary>
	private static string CompanionsLevel(Dictionary<string, object> args)
	{
		if (ZNetScene.instance == null)
		{
			return Fail("the world isn't loaded");
		}
		Player player = Player.m_localPlayer;
		if (player == null)
		{
			return Fail("no player in the world");
		}
		CompanionSetup target = null;
		string name = (Json.Str(args, "name") ?? string.Empty).Trim();
		foreach (CompanionSetup setup in CompanionSetup.AllCompanions)
		{
			if (setup == null)
			{
				continue;
			}
			if (name.Length == 0)
			{
				if (target == null)
				{
					target = setup;
				}
				continue;
			}
			ZNetView nv = setup.GetComponent<ZNetView>();
			ZDO zdo = nv != null ? nv.GetZDO() : null;
			if (string.Equals(zdo != null ? zdo.GetString(CompanionSetup.NameHash, string.Empty) : string.Empty, name, StringComparison.OrdinalIgnoreCase))
			{
				target = setup;
				break;
			}
		}
		if (target == null)
		{
			return Fail(name.Length > 0 ? "no companion named '" + name + "'" : "no companion in the world");
		}
		Vector3 hint;
		if (args.ContainsKey("x") || args.ContainsKey("z"))
		{
			hint = new Vector3(Json.Float(args, "x", player.transform.position.x), Json.Float(args, "y", player.transform.position.y), Json.Float(args, "z", player.transform.position.z));
		}
		else if (!BuildFindCommand.Hint(player, out hint))
		{
			Vector3 forward = player.transform.forward;
			forward.y = 0f;
			forward = forward.sqrMagnitude < 0.01f ? Vector3.forward : forward.normalized;
			Vector3 flat = player.transform.position + forward * 3f;
			float y = ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(flat) : flat.y;
			hint = new Vector3(flat.x, y, flat.z);
		}
		float size = Mathf.Clamp(Json.Float(args, "size", 8f), 2f, 20f);
		MHR_Owner.TakeControl(target);
		bool started = MHR_Level.Start(target, hint, size);
		return Ok(new Dictionary<string, object>
		{
			{ "started", started },
			{ "size", size },
			{ "name", target.GetComponent<ZNetView>()?.GetZDO()?.GetString(CompanionSetup.NameHash, "Companion") },
			{ "hint", new Dictionary<string, object> { { "x", hint.x }, { "y", hint.y }, { "z", hint.z } } }
		});
	}

	private static string CompanionsBuild(Dictionary<string, object> args)
	{
		if (ZNetScene.instance == null)
		{
			return Fail("the world isn't loaded");
		}
		Player player = Player.m_localPlayer;
		if (player == null)
		{
			return Fail("no player in the world");
		}
		string prefab = (Json.Str(args, "prefab") ?? string.Empty).Trim();
		string planName = (Json.Str(args, "plan") ?? string.Empty).Trim();
		BuildTemplates.Template template = null;
		if (planName.Length > 0)
		{
			// E6: a whole structure (a template) instead of one piece
			template = BuildTemplates.ByName(planName) ?? BuildTemplates.Resolve(planName);
			if (template == null)
			{
				return Fail("unknown plan '" + planName + "'");
			}
			foreach (BuildTemplates.Step step in template.Ordered())
			{
				Piece stepPiece = BuildProbe.PieceOf(step.Prefab);
				if (stepPiece == null)
				{
					return Fail("no Piece component on '" + step.Prefab + "'");
				}
				if (player != null && !player.IsRecipeKnown(stepPiece.m_name))
				{
					return Fail("the player has not unlocked the recipe of '" + step.Prefab + "'");
				}
			}
		}
		else
		{
			if (prefab.Length == 0)
			{
				return Fail("prefab or plan is required (e.g. piece_workbench, plan=hut)");
			}
			if (!BuildCatalog.Allowed(prefab))
			{
				return Fail("'" + prefab + "' is not in the build allow-list");
			}
			if (BuildProbe.PieceOf(prefab) == null)
			{
				return Fail("no Piece component on '" + prefab + "'");
			}
			// the same recipe gate the chat order uses: the companion does not build what the player has not unlocked
			Piece bedPiece = BuildProbe.PieceOf(prefab);
			if (player != null && bedPiece != null && !player.IsRecipeKnown(bedPiece.m_name))
			{
				return Fail("the player has not unlocked the recipe of '" + prefab + "'");
			}
		}
		CompanionSetup target = null;
		string name = (Json.Str(args, "name") ?? string.Empty).Trim();
		foreach (CompanionSetup setup in CompanionSetup.AllCompanions)
		{
			if (setup == null)
			{
				continue;
			}
			if (name.Length == 0)
			{
				if (target == null)
				{
					target = setup;
				}
				continue;
			}
			ZNetView nv = setup.GetComponent<ZNetView>();
			ZDO zdo = nv != null ? nv.GetZDO() : null;
			string theirName = zdo != null ? zdo.GetString(CompanionSetup.NameHash, string.Empty) : string.Empty;
			if (string.Equals(theirName, name, StringComparison.OrdinalIgnoreCase))
			{
				target = setup;
				break;
			}
		}
		if (target == null)
		{
			return Fail(name.Length > 0 ? "no companion named '" + name + "'" : "no companion in the world");
		}
		Vector3 hint;
		if (args.ContainsKey("x") || args.ContainsKey("z"))
		{
			// a test may name the spot (E6: the canopy has to be anchored on the bench it shelters)
			hint = new Vector3(Json.Float(args, "x", player.transform.position.x), Json.Float(args, "y", player.transform.position.y), Json.Float(args, "z", player.transform.position.z));
		}
		else if (!BuildFindCommand.Hint(player, out hint))
		{
			Vector3 forward = player.transform.forward;
			forward.y = 0f;
			forward = forward.sqrMagnitude < 0.01f ? Vector3.forward : forward.normalized;
			Vector3 flat = player.transform.position + forward * 3f;
			float y = ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(flat) : flat.y;
			hint = new Vector3(flat.x, y, flat.z);
		}
		MHR_Owner.TakeControl(target);
		// the same leash the chat order applies (CompanionOrders.Build): the harness must behave like the order
		float max = ModConfig.BuildMaxDistanceFromHome != null ? ModConfig.BuildMaxDistanceFromHome.Value : 40f;
		if (max > 0f && target.HasHomePosition())
		{
			float fromHome = Vector3.Distance(target.GetHomePosition(), hint);
			if (fromHome > max)
			{
				return Fail("the spot is " + Mathf.RoundToInt(fromHome) + " m from the companion's home (max " + Mathf.RoundToInt(max) + " m)");
			}
		}
		bool started = template != null ? MHR_Build.StartPlan(target, template, hint) : MHR_Build.Start(target, prefab, hint);
		return Ok(new Dictionary<string, object>
		{
			{ "started", started },
			{ "prefab", template != null ? template.Name : prefab },
			{ "plan", template != null ? template.Name : null },
			{ "pieces", template != null ? template.Steps.Length : 1 },
			{ "name", target.GetComponent<ZNetView>()?.GetZDO()?.GetString(CompanionSetup.NameHash, "Companion") },
			{ "hint", new Dictionary<string, object> { { "x", hint.x }, { "y", hint.y }, { "z", hint.z } } }
		});
	}

	/// <summary>Trilha E (E4): sets the companion's home where it stands (the same call the wheel's "set home here"
	/// makes), so the build leash (<c>[Building] MaxDistanceFromHome</c>) can be exercised from the outside.</summary>
	private static string CompanionsSetHome(Dictionary<string, object> args)
	{
		if (ZNetScene.instance == null)
		{
			return Fail("the world isn't loaded");
		}
		string name = (Json.Str(args, "name") ?? string.Empty).Trim();
		CompanionSetup target = null;
		foreach (CompanionSetup setup in CompanionSetup.AllCompanions)
		{
			if (setup == null)
			{
				continue;
			}
			if (name.Length == 0)
			{
				if (target == null)
				{
					target = setup;
				}
				continue;
			}
			ZNetView nv = setup.GetComponent<ZNetView>();
			ZDO zdo = nv != null ? nv.GetZDO() : null;
			if (string.Equals(zdo != null ? zdo.GetString(CompanionSetup.NameHash, string.Empty) : string.Empty, name, StringComparison.OrdinalIgnoreCase))
			{
				target = setup;
				break;
			}
		}
		if (target == null)
		{
			return Fail(name.Length > 0 ? "no companion named '" + name + "'" : "no companion in the world");
		}
		MHR_Owner.TakeControl(target);
		target.SetHomePosition(target.transform.position);
		Vector3 pos = target.transform.position;
		return Ok(new Dictionary<string, object>
		{
			{ "name", target.GetComponent<ZNetView>()?.GetZDO()?.GetString(CompanionSetup.NameHash, "Companion") },
			{ "x", pos.x },
			{ "y", pos.y },
			{ "z", pos.z }
		});
	}

	/// <summary>Dev setup right after entering the world: dev commands (toggled only once - the command flips a bool),
	/// the time of day (0.5 = midday) and god mode, so the agent can drive the player without dying.</summary>
	private static string WorldSetup(Dictionary<string, object> args)
	{
		if (global::Console.instance == null)
		{
			return Fail("the console is not available");
		}
		if (!global::Console.m_cheat)
		{
			global::Console.instance.TryRunCommand("devcommands", silentFail: true);
		}
		float fraction = Mathf.Clamp(Json.Float(args, "time", 0.5f), 0f, 0.999f);
		global::Console.instance.TryRunCommand("tod " + fraction.ToString(CultureInfo.InvariantCulture), silentFail: true);
		bool wantGod = Json.Bool(args, "god", true);
		global::Console.instance.TryRunCommand(wantGod ? "god on" : "god off", silentFail: true);
		// Valheim 1.0's god mode does NOT cancel damage: it clamps a killing blow to 1 HP (Character.cs). Heal after
		// it so the bar is full and the player is not stuck at 1.
		global::Console.instance.TryRunCommand("heal", silentFail: true);
		Player p = Player.m_localPlayer;
		return Ok(new Dictionary<string, object>
		{
			{ "cheats", global::Console.m_cheat },
			{ "god", p != null && p.InGodMode() },
			{ "hp", p != null ? p.GetHealth() : 0f },
			{ "time", EnvMan.instance != null ? EnvMan.instance.GetDayFraction() : 0f }
		});
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
		// 'spawn', 'tod', 'god'... are cheats: the game only runs them after 'devcommands', which is a toggle.
		if (Json.Bool(args, "cheats", true) && !global::Console.m_cheat)
		{
			global::Console.instance.TryRunCommand("devcommands", silentFail: true);
		}
		List<string> before = ChatBuffer();
		global::Console.instance.TryRunCommand(command, silentFail: false);
		List<string> output = NewLines(ChatBuffer(), before);
		bool failed = output.Exists(line =>
			line.IndexOf("not a recognized command", StringComparison.OrdinalIgnoreCase) >= 0 ||
			line.IndexOf("not valid in the current context", StringComparison.OrdinalIgnoreCase) >= 0);
		Dictionary<string, object> data = new Dictionary<string, object>
		{
			{ "command", command },
			{ "ok", !failed },
			{ "cheats", global::Console.m_cheat },
			{ "output", output }
		};
		// 'spawn <prefab> [count]': say up front whether the prefab exists, so a typo is not a silent no-op.
		string[] parts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length >= 2 && parts[0].Equals("spawn", StringComparison.OrdinalIgnoreCase) && ZNetScene.instance != null)
		{
			data["prefab"] = parts[1];
			data["prefabFound"] = ZNetScene.instance.GetPrefab(parts[1]) != null;
		}
		return Ok(data);
	}

	/// <summary>A copy of the console chat buffer (Terminal.m_chatBuffer, protected): the commands write their
	/// answers there, and diffing before/after is how the bridge reports what a command said.</summary>
	private static List<string> ChatBuffer()
	{
		FieldInfo field = typeof(Terminal).GetField("m_chatBuffer", BindingFlags.Instance | BindingFlags.NonPublic);
		List<string> buffer = field != null && global::Console.instance != null
			? field.GetValue(global::Console.instance) as List<string>
			: null;
		return buffer != null ? new List<string>(buffer) : new List<string>();
	}

	/// <summary>The lines the command added: everything after the last line of the previous snapshot (the buffer
	/// keeps 300 lines, so it can also have rotated).</summary>
	private static List<string> NewLines(List<string> after, List<string> before)
	{
		if (before.Count == 0)
		{
			return after;
		}
		int index = after.LastIndexOf(before[before.Count - 1]);
		return index >= 0 ? after.GetRange(index + 1, after.Count - index - 1) : after;
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

	/// <summary>
	/// Trilha E (E5): turns the player's view. The build order takes its <c>hint</c> from the aim ray
	/// (<see cref="BuildFindCommand.Hint"/>), so a test that wants the piece at the player's feet has to look down -
	/// the first E5 run built the bench 42 m away because the camera was on the horizon. It goes through the game's
	/// own <c>Player.SetMouseLook</c>, so the eye, the camera and the placement ghost all follow.
	/// </summary>
	private static string PlayerLook(Dictionary<string, object> args)
	{
		Player p = Player.m_localPlayer;
		if (p == null)
		{
			return Fail("not in game");
		}
		float down = Json.Float(args, "down", 0f);
		float right = Json.Float(args, "right", 0f);
		// SetMouseLook SUBTRACTS the y of the delta from the pitch, so a positive y looks UP - "down" is passed
		// negated (the game's own mouse input does the same with the screen delta)
		p.SetMouseLook(new Vector2(right, -down));
		Vector3 forward = p.m_eye != null ? p.m_eye.forward : p.transform.forward;
		Dictionary<string, object> result = new Dictionary<string, object>
		{
			{ "down", down },
			{ "right", right },
			{ "forward", new Dictionary<string, object> { { "x", forward.x }, { "y", forward.y }, { "z", forward.z } } }
		};
		if (Physics.Raycast(p.m_eye.position, forward, out RaycastHit hit, 50f, BuildProbe.PlaceMask(), QueryTriggerInteraction.Ignore))
		{
			result["distance"] = hit.distance;
			result["x"] = hit.point.x;
			result["y"] = hit.point.y;
			result["z"] = hit.point.z;
		}
		return Ok(result);
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

	/// <summary>A companion's inventory (the same stack list as the player's), so a test can confirm what it carries.</summary>
	private static string CompanionInventory(Dictionary<string, object> args)
	{
		CompanionSetup setup = FindLiveCompanion(Json.Str(args, "name"));
		if (setup == null)
		{
			return Fail("companion not found (or dead)");
		}
		Humanoid humanoid = setup.GetComponent<Humanoid>();
		Inventory inv = humanoid != null ? humanoid.GetInventory() : null;
		if (inv == null)
		{
			return Fail("the companion has no inventory");
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
				{ "grid", data.m_gridPos.x + "," + data.m_gridPos.y }
			});
		}
		return Ok(new Dictionary<string, object>
		{
			{ "name", humanoid != null ? humanoid.m_name : setup.name },
			{ "stacks", stacks },
			{ "totalStacks", inv.NrOfItems() }
		});
	}

	/// <summary>Dismisses a companion exactly like the radial menu: the gear goes to a tombstone and the body is
	/// removed (CompanionRadialMenu.MHR_DespawnConfirmed).</summary>
	private static string CompanionsDismiss(Dictionary<string, object> args)
	{
		CompanionSetup setup = FindLiveCompanion(Json.Str(args, "name"));
		if (setup == null)
		{
			return Fail("companion not found (or dead)");
		}
		ZNetView nview = setup.GetComponent<ZNetView>();
		if (nview == null)
		{
			return Fail("the companion has no ZNetView");
		}
		string name = nview.GetZDO() != null ? nview.GetZDO().GetString(CompanionSetup.NameHash, "Companion") : "Companion";
		setup.PrepareForDespawn();
		CompanionRest rest = setup.GetComponent<CompanionRest>();
		if (rest != null)
		{
			rest.MHR_ReleaseBedForDespawn();
		}
		setup.DespawnWithTombstone();
		Humanoid humanoid = setup.GetComponent<Humanoid>();
		if (humanoid != null)
		{
			humanoid.m_onDeath = null;
		}
		nview.Destroy();
		return Ok(new Dictionary<string, object> { { "name", name }, { "dismissed", true } });
	}

	/// <summary>The tombstones loaded around the player, with what decides the vanilla despawn (item count, ZDO owner
	/// and whether the container is in use) plus whether it carries companion resurrection data.</summary>
	private static string WorldTombstones(Dictionary<string, object> args)
	{
		Player p = Player.m_localPlayer;
		List<object> graves = new List<object>();
		foreach (TombStone tomb in UnityEngine.Object.FindObjectsByType<TombStone>(UnityEngine.FindObjectsSortMode.None))
		{
			if (tomb == null)
			{
				continue;
			}
			Container container = tomb.GetComponent<Container>();
			ZNetView nview = tomb.GetComponent<ZNetView>();
			ZDO zdo = nview != null ? nview.GetZDO() : null;
			Vector3 pos = tomb.transform.position;
			List<object> contents = new List<object>();
			if (container != null && container.GetInventory() != null)
			{
				foreach (ItemDrop.ItemData data in container.GetInventory().GetAllItems())
				{
					if (data != null && data.m_shared != null)
					{
						contents.Add(new Dictionary<string, object>
						{
							{ "prefab", data.m_dropPrefab != null ? data.m_dropPrefab.name : null },
							{ "amount", data.m_stack },
							{ "grid", data.m_gridPos.x + "," + data.m_gridPos.y }
						});
					}
				}
			}
			graves.Add(new Dictionary<string, object>
			{
				{ "name", container != null ? container.m_name : tomb.name },
				{ "x", pos.x },
				{ "y", pos.y },
				{ "z", pos.z },
				{ "distance", p != null ? Vector3.Distance(p.transform.position, pos) : 0f },
				{ "items", container != null && container.GetInventory() != null ? container.GetInventory().NrOfItems() : -1 },
				{ "inUse", container != null && container.IsInUse() },
				{ "isOwner", nview != null && nview.IsOwner() },
				{ "owner", zdo != null ? zdo.GetOwner() : 0L },
				{ "tombstoneId", zdo != null ? zdo.GetLong(CompanionSetup.TombstoneIdHash, 0L) : 0L },
				{ "ownerUid", zdo != null ? zdo.GetLong(ZDOVars.s_owner, 0L) : 0L },
				{ "localPlayerId", p != null ? p.GetPlayerID() : 0L },
				{ "respawnPrefab", zdo != null ? zdo.GetString(CompanionTombstoneResurrect.RespawnPrefabHash, "") : "" },
				{ "contents", contents }
			});
		}
		return Ok(new Dictionary<string, object> { { "count", graves.Count }, { "tombstones", graves } });
	}

	/// <summary>Loots the nearest tombstone with items through the game's own entry point
	/// (<see cref="TombStone.Interact"/>, the same method the E key calls - it takes everything when it fits).</summary>
	private static string WorldTombstoneTake(Dictionary<string, object> args)
	{
		Player p = Player.m_localPlayer;
		if (p == null)
		{
			return Fail("no player in the world");
		}
		TombStone best = null;
		float bestDistance = float.MaxValue;
		foreach (TombStone tomb in UnityEngine.Object.FindObjectsByType<TombStone>(UnityEngine.FindObjectsSortMode.None))
		{
			if (tomb == null)
			{
				continue;
			}
			Container container = tomb.GetComponent<Container>();
			if (container == null || container.GetInventory() == null || container.GetInventory().NrOfItems() == 0)
			{
				continue;
			}
			float d = Vector3.Distance(p.transform.position, tomb.transform.position);
			if (d < bestDistance)
			{
				bestDistance = d;
				best = tomb;
			}
		}
		if (best == null)
		{
			return Fail("no tombstone with items nearby");
		}
		Container grave = best.GetComponent<Container>();
		int before = grave != null && grave.GetInventory() != null ? grave.GetInventory().NrOfItems() : 0;
		// TakeAll is the call the game makes when the grave fits in the inventory - and it does NOT touch
		// Container.m_inUse, unlike Interact (which opens the container). Try it first so the test matches the
		// player path that lets the tombstone despawn.
		bool used = grave != null && grave.TakeAll(p);
		int after = grave != null && grave.GetInventory() != null ? grave.GetInventory().NrOfItems() : 0;
		if (after == before)
		{
			used = best.Interact(p, hold: false, alt: false);
			after = grave != null && grave.GetInventory() != null ? grave.GetInventory().NrOfItems() : 0;
		}
		return Ok(new Dictionary<string, object>
		{
			{ "name", grave != null ? grave.m_name : best.name },
			{ "distance", bestDistance },
			{ "used", used },
			{ "stacksBefore", before },
			{ "stacksAfter", after }
		});
	}

	/// <summary>Destroys the companion tombstones around the player (dev cleanup for tests); 'includePlayer' also
	/// removes the player's own tombstones.</summary>
	private static string WorldTombstoneDestroy(Dictionary<string, object> args)
	{
		bool includePlayer = Json.Bool(args, "includePlayer", false);
		int destroyed = 0;
		List<object> names = new List<object>();
		foreach (TombStone tomb in UnityEngine.Object.FindObjectsByType<TombStone>(UnityEngine.FindObjectsSortMode.None))
		{
			if (tomb == null)
			{
				continue;
			}
			ZNetView nview = tomb.GetComponent<ZNetView>();
			ZDO zdo = nview != null ? nview.GetZDO() : null;
			if (zdo == null)
			{
				continue;
			}
			if (zdo.GetLong(CompanionSetup.TombstoneIdHash, 0L) == 0 && !includePlayer)
			{
				continue;
			}
			Container container = tomb.GetComponent<Container>();
			names.Add(container != null ? container.m_name : tomb.name);
			if (!nview.IsOwner())
			{
				nview.ClaimOwnership();
			}
			nview.Destroy();
			destroyed++;
		}
		return Ok(new Dictionary<string, object> { { "destroyed", destroyed }, { "names", names } });
	}

	/// <summary>Diagnostics for the tombstone despawn: the component state, whether the repeating UpdateDespawn is
	/// running, the ZNetView validity and, with 'tick', a forced call to the private UpdateDespawn.</summary>
	private static string WorldTombstoneDiag(Dictionary<string, object> args)
	{
		bool tick = Json.Bool(args, "tick", false);
		List<object> rows = new List<object>();
		MethodInfo update = typeof(TombStone).GetMethod("UpdateDespawn", BindingFlags.Instance | BindingFlags.NonPublic);
		foreach (TombStone tomb in UnityEngine.Object.FindObjectsByType<TombStone>(UnityEngine.FindObjectsSortMode.None))
		{
			if (tomb == null)
			{
				continue;
			}
			ZNetView nview = tomb.GetComponent<ZNetView>();
			Container container = tomb.GetComponent<Container>();
			ZDO zdo = nview != null ? nview.GetZDO() : null;
			if (zdo == null || zdo.GetLong(CompanionSetup.TombstoneIdHash, 0L) == 0)
			{
				continue;
			}
			Dictionary<string, object> row = new Dictionary<string, object>
			{
				{ "name", container != null ? container.m_name : tomb.name },
				{ "enabled", tomb.enabled },
				{ "activeInHierarchy", tomb.gameObject.activeInHierarchy },
				{ "isInvoking", tomb.IsInvoking("UpdateDespawn") },
				{ "nviewValid", nview != null && nview.IsValid() },
				{ "isOwner", nview != null && nview.IsOwner() },
				{ "inUse", container != null && container.IsInUse() },
				{ "items", container != null && container.GetInventory() != null ? container.GetInventory().NrOfItems() : -1 }
			};
			if (tick && update != null)
			{
				update.Invoke(tomb, null);
				row["ticked"] = true;
				row["itemsAfterTick"] = container != null && container.GetInventory() != null ? container.GetInventory().NrOfItems() : -1;
			}
			rows.Add(row);
		}
		return Ok(new Dictionary<string, object> { { "count", rows.Count }, { "tombstones", rows } });
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
		// Best effort: the synthetic events go straight into the Input System, which does not need the window focused.
		InputSim.FocusGame();
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
		// Best effort: the synthetic events go straight into the Input System, which does not need the window focused.
		InputSim.FocusGame();
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
		// Best effort: the synthetic events go straight into the Input System, which does not need the window focused.
		InputSim.FocusGame();
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
		// Best effort: the synthetic events go straight into the Input System, which does not need the window focused.
		InputSim.FocusGame();
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
