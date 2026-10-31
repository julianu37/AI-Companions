using System;
using System.Collections.Generic;
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

	internal static bool Ready;

	internal static Pathfinding.AgentType AgentOrHumanoid => Ready ? Agent : Pathfinding.AgentType.Humanoid;

	private static readonly MethodInfo s_addAgent = AccessTools.Method(typeof(Pathfinding), "AddAgent");

	private static readonly MethodInfo s_getSettings = AccessTools.Method(typeof(Pathfinding), "GetSettings");

	private static readonly FieldInfo s_layers = AccessTools.Field(typeof(Pathfinding), "m_layers");

	private static readonly FieldInfo s_waterLayers = AccessTools.Field(typeof(Pathfinding), "m_waterLayers");

	private static readonly FieldInfo s_tileSize = AccessTools.Field(typeof(Pathfinding), "m_tileSize");

	private static readonly FieldInfo s_buildOperation = AccessTools.Field(typeof(Pathfinding), "m_buildOperation");

	private static readonly FieldInfo s_buildTile = AccessTools.Field(typeof(Pathfinding), "m_buildTile");

	private static FieldInfo s_build, s_tileId, s_center, s_data;

	[HarmonyPatch(typeof(Pathfinding), "SetupAgents")]
	private static class SetupAgents_Patch
	{
		private static void Postfix(Pathfinding __instance)
		{
			Ready = false;
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
				if (s_tileId == null)
				{
					Type t = tile.GetType();
					s_tileId = AccessTools.Field(t, "m_tile");
					s_center = AccessTools.Field(t, "m_center");
					s_data = AccessTools.Field(t, "m_data");
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
				NavMeshBuilder.CollectSources(world, layers, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
				int doors = sources.RemoveAll((NavMeshBuildSource s) => s.component != null && s.component.GetComponentInParent<Door>() != null);
				List<NavMeshBuildSource> waterSources = new List<NavMeshBuildSource>();
				NavMeshBuilder.CollectSources(world, water, NavMeshCollectGeometry.PhysicsColliders, 3, markups, waterSources);
				sources.AddRange(waterSources);
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
}
