using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace Companions;

// 1.4.85: the companions' own route map. Valheim's Humanoid map treats a closed door as a wall and only steps up
// 0.3 m, so routes stop at every closed door and most stairs aren't connected (1.4.83/1.4.84 tests: the farm's chests
// up the stairs in the house, runs round the base for the steps out front). This adds one more agent type, built like
// Humanoid but leaving door colliders out and climbing 0.6 m. Tiles are only built where something asks for a route
// with it, so only around companions. MHR_Mover opens doors as it reaches them and hops the steps.
internal static class MHR_CompanionNavMesh
{
	internal const Pathfinding.AgentType Agent = (Pathfinding.AgentType)40;

	// P2 (multi-build, 03/11/2026): a piece going up or down marks its tiles dirty; the flush is coalesced into one
	// every this many seconds (a house of 985 pieces would otherwise be thousands of builds).
	private const float FlushDelay = 0.35f;

	// P2: the game rebuilds a tile when `m_pokeTime - m_buildTime > m_updateInterval` (5 s, Pathfinding.cs:101/:132).
	// Putting the build time this far behind makes the tile eligible on the very next 0.1 s tick without taking the
	// throttle away. -1000 would nullify the 5 s and turn 985 pieces into ~9 000 builds.
	private const float BuildBehind = 5.1f;

	internal static bool Ready;

	internal static Pathfinding.AgentType AgentOrHumanoid => Ready ? Agent : Pathfinding.AgentType.Humanoid;

	// P2 instrumentation for the DevBridge (`nav.stats`); reset on SetupAgents, so the numbers are "this world".
	internal static int Pokes;
	internal static int Rebuilds;
	internal static float MaxCollectMs;

	private static readonly MethodInfo s_addAgent = AccessTools.Method(typeof(Pathfinding), "AddAgent");

	private static readonly MethodInfo s_getSettings = AccessTools.Method(typeof(Pathfinding), "GetSettings");

	private static readonly FieldInfo s_layers = AccessTools.Field(typeof(Pathfinding), "m_layers");

	private static readonly FieldInfo s_waterLayers = AccessTools.Field(typeof(Pathfinding), "m_waterLayers");

	private static readonly FieldInfo s_tileSize = AccessTools.Field(typeof(Pathfinding), "m_tileSize");

	private static readonly FieldInfo s_buildOperation = AccessTools.Field(typeof(Pathfinding), "m_buildOperation");

	private static readonly FieldInfo s_buildTile = AccessTools.Field(typeof(Pathfinding), "m_buildTile");

	// P2: the tiles the game already knows (read-only: a tile is never created here)
	private static readonly FieldInfo s_tiles = AccessTools.Field(typeof(Pathfinding), "m_tiles");

	private static FieldInfo s_build, s_tileId, s_center, s_data;

	// P2: the two clocks `Pathfinding.Buildtiles` compares, plus the instance (for the DevBridge view of a tile)
	private static FieldInfo s_pokeTime, s_buildTime, s_instance;

	private static PropertyInfo s_instanceValid;

	// P2: the agent's query id/mask, kept from SetupAgents for `nav.probe` (`NavMesh.SamplePosition` with our filter)
	private static int s_agentTypeId = -1;

	private static int s_areaMask = -1;

	// P2: the tiles waiting for the next flush (main thread only), and the state of the feature itself
	private static readonly HashSet<Vector3Int> s_dirty = new HashSet<Vector3Int>();

	private static float s_flushAt;

	private static float s_tileSizeValue;

	private static bool s_invalidateBroken;

	[HarmonyPatch(typeof(Pathfinding), "SetupAgents")]
	private static class SetupAgents_Patch
	{
		private static void Postfix(Pathfinding __instance)
		{
			Ready = false;
			// P2: a fresh world starts clean - nothing pending, the tile size read again and the feature given a new try
			s_dirty.Clear();
			s_flushAt = 0f;
			s_tileSizeValue = 0f;
			s_invalidateBroken = false;
			Pokes = 0;
			Rebuilds = 0;
			MaxCollectMs = 0f;
			s_agentTypeId = -1;
			s_areaMask = -1;
			try
			{
				if (ModConfig.CompanionNavMesh != null && !ModConfig.CompanionNavMesh.Value)
				{
					return;
				}
				object humanoid = s_getSettings.Invoke(__instance, new object[1] { Pathfinding.AgentType.Humanoid });
				object mine = s_addAgent.Invoke(__instance, new object[2] { Agent, humanoid });
				s_build = AccessTools.Field(mine.GetType(), "m_build");
				NavMeshBuildSettings build = (NavMeshBuildSettings)s_build.GetValue(mine);
				build.agentClimb = 0.6f;
				s_build.SetValue(mine, build);
				// P2: what `nav.probe` asks `NavMesh.SamplePosition` with - the same agent and area mask a route uses
				s_agentTypeId = build.agentTypeID;
				s_areaMask = AreaMaskOf(mine);
				Ready = true;
				if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log?.LogInfo("[Move] Companion route map ready (doors don't block, steps up to 0.6 m)");
			}
			catch (Exception ex)
			{
				CompanionsPlugin.Log?.LogWarning("[Move] Couldn't set up the companion route map, using the normal one: " + ex.Message);
			}
		}
	}

	// the same as Pathfinding.BuildTile for this one agent type, minus the doors
	[HarmonyPatch(typeof(Pathfinding), "BuildTile")]
	private static class BuildTile_Patch
	{
		private static bool Prefix(Pathfinding __instance, object tile)
		{
			if (!Ready || tile == null)
			{
				return true;
			}
			try
			{
				if (!EnsureTileFields())
				{
					return true;
				}
				Vector3Int id = (Vector3Int)s_tileId.GetValue(tile);
				if (id.z != (int)Agent)
				{
					return true;
				}
				Vector3 center = (Vector3)s_center.GetValue(tile);
				float size = (float)s_tileSize.GetValue(__instance);
				int layers = ((LayerMask)s_layers.GetValue(__instance)).value;
				int water = ((LayerMask)s_waterLayers.GetValue(__instance)).value;
				object settings = s_getSettings.Invoke(__instance, new object[1] { Agent });
				NavMeshBuildSettings build = (NavMeshBuildSettings)s_build.GetValue(settings);
				Bounds world = new Bounds(center, new Vector3(size, 6000f, size));
				Bounds local = new Bounds(Vector3.zero, new Vector3(size, 6000f, size));
				List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
				List<NavMeshBuildMarkup> markups = new List<NavMeshBuildMarkup>();
				// P2: the synchronous part of a tile build is what the main thread pays, so it is what `nav.stats` shows
				long collectStart = Stopwatch.GetTimestamp();
				NavMeshBuilder.CollectSources(world, layers, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
				int doors = sources.RemoveAll((NavMeshBuildSource s) => s.component != null && s.component.GetComponentInParent<Door>() != null);
				List<NavMeshBuildSource> waterSources = new List<NavMeshBuildSource>();
				NavMeshBuilder.CollectSources(world, water, NavMeshCollectGeometry.PhysicsColliders, 3, markups, waterSources);
				sources.AddRange(waterSources);
				float collectMs = (float)((Stopwatch.GetTimestamp() - collectStart) * 1000.0 / Stopwatch.Frequency);
				Rebuilds++;
				if (collectMs > MaxCollectMs) MaxCollectMs = collectMs;
				NavMeshData data = (NavMeshData)s_data.GetValue(tile);
				if (data == null)
				{
					data = new NavMeshData();
					data.position = center;
					s_data.SetValue(tile, data);
				}
				s_buildOperation.SetValue(__instance, NavMeshBuilder.UpdateNavMeshDataAsync(data, build, sources, local));
				s_buildTile.SetValue(__instance, tile);
				return false;
			}
			catch (Exception ex)
			{
				Ready = false;
				CompanionsPlugin.Log?.LogWarning("[Move] Companion route map failed, using the normal one: " + ex.Message);
				return true;
			}
		}
	}

	// P2: a piece placed by the player (Player.PlacePiece) or by a companion (BuildExecutor calls it by hand) - the
	// hook that makes the new floor part of the companions' route straight away
	[HarmonyPatch(typeof(WearNTear), "OnPlaced")]
	private static class OnPlaced_Patch
	{
		private static void Postfix(WearNTear __instance)
		{
			if (!Ready || __instance == null)
			{
				return;
			}
			InvalidateAround(__instance.transform.position);
		}
	}

	// P2: a piece that comes down (damage/RPC_Remove). `Destroy` is private and always ends in ZNetScene.Destroy; the
	// prefix is the point where the transform is still valid. It never suppresses the original.
	[HarmonyPatch(typeof(WearNTear), "Destroy", new[] { typeof(HitData), typeof(bool) })]
	private static class Destroy_Patch
	{
		private static void Prefix(WearNTear __instance)
		{
			if (!Ready || __instance == null)
			{
				return;
			}
			InvalidateAround(__instance.transform.position);
		}
	}

	/// <summary>
	/// Makes the companions' route see a piece straight away. The game <b>never</b> invalidates a tile when a piece goes
	/// up or down (see <c>docs/re/navmesh-tiles.md</c>): it only rebuilds the tiles something asked a route for, at most
	/// once every 5 s, so a floor placed a moment ago is not on the map yet and a stair that came down is still on it.
	/// This marks the tiles around <paramref name="pos"/> the way <c>Pathfinding.PokeArea</c> does, <b>coalesced</b> into
	/// one flush every 0,35 s (a house of 985 pieces would be thousands of builds).
	///
	/// <para><b>Only tiles that already exist are touched</b> (<see cref="Flush"/> reads the game's dictionary with
	/// <c>Contains</c>): <c>GetNavTile</c>/<c>PokeTile</c> create the tile and would start a build of a whole 32x32 m
	/// tile. Fail-open: a reflection problem switches the feature off for the rest of the world and never breaks the
	/// caller (placing a piece matters more than the route).</para>
	/// </summary>
	internal static void InvalidateAround(Vector3 pos, float radius = 2f)
	{
		if (!Ready || s_invalidateBroken)
		{
			return;
		}
		// [Movement] PathingOnBuild off = the game's own delay, nothing else changes
		if (ModConfig.PathingOnBuild != null && !ModConfig.PathingOnBuild.Value)
		{
			return;
		}
		if (Pathfinding.instance == null)
		{
			return;
		}
		try
		{
			if (s_tileSizeValue <= 0f)
			{
				object raw = s_tileSize.GetValue(Pathfinding.instance);
				if (raw is float size && size > 0f)
				{
					s_tileSizeValue = size;
				}
			}
			if (s_tileSizeValue <= 0f)
			{
				return;
			}
			float r = radius > 0f ? radius : 0f;
			Mark(pos.x, pos.z);
			Mark(pos.x + r, pos.z);
			Mark(pos.x - r, pos.z);
			Mark(pos.x, pos.z + r);
			Mark(pos.x, pos.z - r);
			// the four corners too: a piece standing on a tile seam pokes into the diagonal tile as well
			Mark(pos.x + r, pos.z + r);
			Mark(pos.x + r, pos.z - r);
			Mark(pos.x - r, pos.z + r);
			Mark(pos.x - r, pos.z - r);
			if (s_flushAt <= 0f)
			{
				s_flushAt = Time.time + FlushDelay;
			}
		}
		catch (Exception ex)
		{
			s_invalidateBroken = true;
			CompanionsPlugin.Log?.LogWarning("[Move] não consegui adiantar o tile da navmesh da obra (desligado nesta sessão): " + ex.Message);
		}
	}

	/// <summary>The coalesced flush, driven from <c>CompanionsPlugin.Update</c> (main thread only). Does nothing while
	/// no piece changed.</summary>
	internal static void Tick()
	{
		if (s_dirty.Count == 0)
		{
			return;
		}
		if (Time.time < s_flushAt)
		{
			return;
		}
		Flush();
	}

	/// <summary>The tiles of the companions' agent around <paramref name="center"/> (DevBridge <c>nav.tiles</c>). It
	/// never creates a tile - a read of the game's own dictionary.</summary>
	internal static List<Dictionary<string, object>> TilesAround(Vector3 center, float radius)
	{
		List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
		Pathfinding pf = Pathfinding.instance;
		if (pf == null)
		{
			return list;
		}
		if (!Ready || !EnsureTileFields())
		{
			return list;
		}
		IDictionary tiles = s_tiles != null ? s_tiles.GetValue(pf) as IDictionary : null;
		if (tiles == null)
		{
			return list;
		}
		foreach (DictionaryEntry entry in tiles)
		{
			object rawKey = entry.Key;
			if (!(rawKey is Vector3Int) || entry.Value == null)
			{
				continue;
			}
			Vector3Int key = (Vector3Int)rawKey;
			if (key.z != (int)Agent)
			{
				continue;
			}
			Vector3 tileCenter = s_center != null ? (Vector3)s_center.GetValue(entry.Value) : Vector3.zero;
			// m_center is GetTilePos: its y is the game's tile plane (2500), so the radius is XZ; the JSON reports
			// the tile's X/Z with the queried y, which is what the player sees
			float tileDx = tileCenter.x - center.x;
			float tileDz = tileCenter.z - center.z;
			if (radius > 0f && Mathf.Sqrt(tileDx * tileDx + tileDz * tileDz) > radius)
			{
				continue;
			}
			list.Add(new Dictionary<string, object>
			{
				{ "id", key.x.ToString(CultureInfo.InvariantCulture) + "," + key.y.ToString(CultureInfo.InvariantCulture) + "," + key.z.ToString(CultureInfo.InvariantCulture) },
				{ "center", new Dictionary<string, object> { { "x", tileCenter.x }, { "y", center.y }, { "z", tileCenter.z } } },
				{ "poke", s_pokeTime != null ? (float)s_pokeTime.GetValue(entry.Value) : 0f },
				{ "build", s_buildTime != null ? (float)s_buildTime.GetValue(entry.Value) : 0f },
				{ "hasData", s_data != null && s_data.GetValue(entry.Value) != null },
				{ "instanceValid", InstanceValid(entry.Value) }
			});
		}
		return list;
	}

	/// <summary>DevBridge <c>nav.probe</c>: the map's own answer for a point, with the companions' agent and area mask
	/// (the same filter a route uses). <c>false</c> = the map does not cover that spot.</summary>
	internal static bool Probe(Vector3 point, float range, out Vector3 hit, out float distance)
	{
		hit = point;
		distance = 0f;
		if (s_agentTypeId < 0)
		{
			return false;
		}
		NavMeshQueryFilter filter = new NavMeshQueryFilter();
		filter.agentTypeID = s_agentTypeId;
		filter.areaMask = s_areaMask > 0 ? s_areaMask : NavMesh.AllAreas;
		if (!NavMesh.SamplePosition(point, out NavMeshHit found, range, filter))
		{
			return false;
		}
		hit = found.position;
		distance = Vector3.Distance(point, hit);
		return true;
	}

	/// <summary>DevBridge <c>nav.stats</c>: the counters of this world (see <see cref="Pokes"/>,
	/// <see cref="Rebuilds"/>, <see cref="MaxCollectMs"/>).</summary>
	internal static Dictionary<string, object> Stats()
	{
		return new Dictionary<string, object>
		{
			{ "ready", Ready },
			{ "broken", s_invalidateBroken },
			{ "pokes", Pokes },
			{ "rebuilds", Rebuilds },
			{ "maxCollectMs", MaxCollectMs },
			{ "dirty", s_dirty.Count },
			{ "agentTypeId", s_agentTypeId },
			{ "areaMask", s_areaMask }
		};
	}

	/// <summary>One dirty tile key at a world X/Z: the game's own key layout is <c>(x, z, agent)</c>.</summary>
	private static void Mark(float worldX, float worldZ)
	{
		s_dirty.Add(new Vector3Int(NavTileMath.Axis(worldX, s_tileSizeValue), NavTileMath.Axis(worldZ, s_tileSizeValue), (int)Agent));
	}

	/// <summary>Hands the pending tiles to the game: <c>m_pokeTime = now</c> and <c>m_buildTime = now - 5,1</c>, so
	/// <c>Buildtiles</c> takes it on its next tick. Only tiles that already exist, never a new one.</summary>
	private static void Flush()
	{
		try
		{
			Pathfinding pf = Pathfinding.instance;
			if (pf == null || !EnsureTileFields())
			{
				return;
			}
			IDictionary tiles = s_tiles.GetValue(pf) as IDictionary;
			if (tiles == null)
			{
				return;
			}
			float now = Time.time;
			int marked = 0;
			foreach (Vector3Int key in s_dirty)
			{
				if (!tiles.Contains(key))
				{
					continue;   // no tile there yet: the game creates it when a route needs it
				}
				object tile = tiles[key];
				if (tile == null)
				{
					continue;
				}
				s_pokeTime.SetValue(tile, now);
				s_buildTime.SetValue(tile, now - BuildBehind);
				marked++;
			}
			Pokes += marked;
			if (marked > 0 && CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log?.LogDebug("[Move] navmesh: " + marked + " tile(s) adiantados para reconstruir (peça colocada ou removida)");
			}
		}
		catch (Exception ex)
		{
			s_invalidateBroken = true;
			CompanionsPlugin.Log?.LogWarning("[Move] a invalidação da navmesh da obra falhou (desligada nesta sessão): " + ex.Message);
		}
		finally
		{
			s_dirty.Clear();
			s_flushAt = 0f;
		}
	}

	/// <summary>The private members of the game's <c>NavMeshTile</c> (a nested private type), resolved once.</summary>
	private static bool EnsureTileFields()
	{
		if (s_tiles != null && s_tileId != null && s_center != null && s_data != null && s_pokeTime != null && s_buildTime != null)
		{
			return true;
		}
		Type type = s_tileId != null ? s_tileId.DeclaringType : typeof(Pathfinding).GetNestedType("NavMeshTile", BindingFlags.Public | BindingFlags.NonPublic);
		if (type == null)
		{
			MarkInvalidateBroken();
			return false;
		}
		if (s_tileId == null) s_tileId = AccessTools.Field(type, "m_tile");
		if (s_center == null) s_center = AccessTools.Field(type, "m_center");
		if (s_data == null) s_data = AccessTools.Field(type, "m_data");
		if (s_pokeTime == null) s_pokeTime = AccessTools.Field(type, "m_pokeTime");
		if (s_buildTime == null) s_buildTime = AccessTools.Field(type, "m_buildTime");
		if (s_instance == null) s_instance = AccessTools.Field(type, "m_instance");
		bool ok = s_tiles != null && s_tileId != null && s_center != null && s_data != null && s_pokeTime != null && s_buildTime != null;
		if (!ok)
		{
			MarkInvalidateBroken();
		}
		return ok;
	}

	/// <summary>One warning, then the feature stays off for the session; <see cref="Ready"/> is untouched so the
	/// companions' own route map keeps working (the invalidation is an optimization, not the map).</summary>
	private static void MarkInvalidateBroken()
	{
		if (s_invalidateBroken)
		{
			return;
		}
		s_invalidateBroken = true;
		CompanionsPlugin.Log?.LogWarning("[Move] Pathfinding/NavMeshTile perderam m_tiles/m_pokeTime/m_buildTime - a navmesh da obra fica desligada nesta sessão");
	}

	private static bool InstanceValid(object tile)
	{
		if (s_instance == null || tile == null)
		{
			return false;
		}
		object instance = s_instance.GetValue(tile);
		if (instance == null)
		{
			return false;
		}
		if (s_instanceValid == null)
		{
			s_instanceValid = instance.GetType().GetProperty("valid");
		}
		return s_instanceValid != null && s_instanceValid.GetValue(instance) is bool valid && valid;
	}

	/// <summary>The area mask of an agent's settings (<c>AgentSettings.m_areaMask</c>, private, defaults to -1 = all).</summary>
	private static int AreaMaskOf(object agentSettings)
	{
		if (agentSettings == null)
		{
			return -1;
		}
		FieldInfo field = AccessTools.Field(agentSettings.GetType(), "m_areaMask");
		if (field == null)
		{
			return -1;
		}
		object value = field.GetValue(agentSettings);
		return value is int mask ? mask : -1;
	}
}
