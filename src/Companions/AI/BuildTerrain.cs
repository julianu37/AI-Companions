using System;
using System.Collections.Generic;
using UnityEngine;

namespace Companions;

/// <summary>
/// Fase E10: <b>leveling the ground before building</b>. A house of 8x8 needs a flat area - the walls and the roof are
/// all at the anchor's height, so a slope leaves pieces floating (measured 01/11/2026: the shelter refused the poles
/// on the low side). The player levels with the <b>hoe</b>; this does the same thing, with the game's own operation.
///
/// <para><b>How the game levels</b> (decompiled 1.0.20): the hoe's menu is a piece table whose pieces carry a
/// <c>TerrainOp</c>; instantiating one calls <c>TerrainOp.Awake</c> -> <c>Heightmap.GetAndCreateTerrainCompiler()
/// .ApplyOperation(op)</c> -> the networked <c>RPC_ApplyOperation</c> -> <c>TerrainComp.LevelTerrain</c>, which sets
/// every heightmap vertex inside the radius (a square when <c>m_square</c>) to the operation's <b>y</b>. The operation
/// then destroys its own object. So leveling is: instantiate the level piece at the points we want, all at the same y
/// - exactly what a player clicking the hoe does.</para>
///
/// <para>Everything is main-thread and null-safe; the prefab is looked up once and cached.</para>
/// </summary>
internal static class BuildTerrain
{
	/// <summary>How much height variation a build site may have (metres) before the ground is leveled.</summary>
	internal const float DefaultTolerance = 0.35f;

	private static bool _looked;
	private static GameObject _level;
	private static float _radius = 2f;
	private static bool _square = true;
	/// <summary>True when the operation is the game's soft level (the smooth one): it moves the terrain by at most 1 m
	/// per operation and needs a few passes.</summary>
	private static bool _soft = true;

	private static void Look()
	{
		if (_looked)
		{
			return;
		}
		_looked = true;
		try
		{
			GameObject hoe = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("Hoe") : null;
			ItemDrop drop = hoe != null ? hoe.GetComponent<ItemDrop>() : null;
			PieceTable table = drop != null && drop.m_itemData != null && drop.m_itemData.m_shared != null ? drop.m_itemData.m_shared.m_buildPieces : null;
			if (table == null || table.m_pieces == null)
			{
				return;
			}
			foreach (GameObject candidate in table.m_pieces)
			{
				if (candidate == null)
				{
					continue;
				}
				Piece piece = candidate.GetComponent<Piece>();
				bool byName = piece != null && piece.m_name == "$piece_levelground";
				// includeInactive: the prefab's children are inactive and the operation lives on one of them
				// (GetComponentInChildren without it returned only the road's paint/smooth operation)
				TerrainOp[] ops = candidate.GetComponentsInChildren<TerrainOp>(true);
				foreach (TerrainOp op in ops)
				{
					if (op == null || op.m_settings == null)
					{
						continue;
					}
					// the game's "level ground" is a SOFT level: it pulls the terrain toward the operation's y with a
					// falloff (TerrainComp.SmoothTerrain), clamped to 1 m per operation - it needs a few passes
					bool level = op.m_settings.m_level;
					bool soft = byName || (op.m_settings.m_smooth && !op.m_settings.m_raise);
					if (!level && !soft)
					{
						continue;
					}
					_level = candidate;
					_radius = Mathf.Max(1f, level ? op.m_settings.m_levelRadius : op.m_settings.m_smoothRadius);
					_square = level && op.m_settings.m_square;
					_soft = !level;
					return;
				}
			}
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[Build] couldn't read the hoe's level piece: " + ex.Message);
		}
	}

	/// <summary>Is the hoe's level piece available? (the hoe is a normal item of the game, not ours).</summary>
	internal static bool Available()
	{
		Look();
		return _level != null;
	}

	/// <summary>The radius of one level operation (metres), as the game's piece declares it.</summary>
	internal static float Radius()
	{
		Look();
		return _radius;
	}

	/// <summary>True when the game's leveling is the soft one (a few passes are needed to bring the ground down).</summary>
	internal static bool Soft()
	{
		Look();
		return _soft;
	}

	/// <summary>
	/// Samples a grid of ground heights over a square area and answers whether it is flat enough. <paramref name="target"/>
	/// is the <b>median</b> height - the height the ground is leveled to (the median survives a rock or a hole at one
	/// corner better than the average).
	/// </summary>
	internal static bool FlatEnough(Vector3 center, float size, float tolerance, out float min, out float max, out float target)
	{
		min = float.MaxValue;
		max = float.MinValue;
		target = center.y;
		List<float> heights = new List<float>();
		const int samples = 5;
		float half = size * 0.5f;
		for (int i = 0; i < samples; i++)
		{
			for (int j = 0; j < samples; j++)
			{
				float x = center.x - half + i * (size / (samples - 1));
				float z = center.z - half + j * (size / (samples - 1));
				float y = ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(new Vector3(x, center.y, z)) : center.y;
				heights.Add(y);
				if (y < min)
				{
					min = y;
				}
				if (y > max)
				{
					max = y;
				}
			}
		}
		heights.Sort();
		target = heights[heights.Count / 2];
		return max - min <= tolerance;
	}

	/// <summary>
	/// Levels the area to <paramref name="targetY"/> by instantiating the hoe's level piece over a grid (the operation
	/// covers a square of its own radius, so the grid is spaced by it). Returns how many operations were applied.
	/// </summary>
	internal static int Level(Vector3 center, float size, float targetY, out string error)
	{
		error = null;
		Look();
		if (_level == null)
		{
			error = "the hoe's level piece was not found";
			return 0;
		}
		// the grid is spaced by the RADIUS on purpose: overlapping operations fight each other (measured 01/11/2026:
		// a 1,5 m grid diverged 0,55 -> 0,95 -> 1,33 -> 2,01 m while a grid spaced by the 3 m radius converged)
		float spacing = _radius;
		int steps = Mathf.Clamp(Mathf.CeilToInt(size / spacing), 1, 12);
		float half = size * 0.5f;
		float cell = size / steps;
		int applied = 0;
		for (int i = 0; i < steps; i++)
		{
			for (int j = 0; j < steps; j++)
			{
				Vector3 pos = new Vector3(center.x - half + (i + 0.5f) * cell, targetY, center.z - half + (j + 0.5f) * cell);
				try
				{
					// the operation applies in Awake (networked RPC) and destroys its own object
					UnityEngine.Object.Instantiate(_level, pos, Quaternion.identity);
					applied++;
				}
				catch (Exception ex)
				{
					error = ex.Message;
					return applied;
				}
			}
		}
		return applied;
	}

	/// <summary>One leveling operation at a point (the swing the job does at each spot).</summary>
	internal static bool ApplyOne(Vector3 point, float targetY)
	{
		Look();
		if (_level == null)
		{
			return false;
		}
		try
		{
			UnityEngine.Object.Instantiate(_level, new Vector3(point.x, targetY, point.z), Quaternion.identity);
			return true;
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[Level] operation failed: " + ex.Message);
			return false;
		}
	}

	/// <summary>The hoe's attack animation, so the companion visibly works the ground (null when there is no hoe).</summary>
	internal static string HoeAnimation(Inventory bag)
	{
		try
		{
			if (bag == null)
			{
				return null;
			}
			ItemDrop.ItemData hoe = bag.GetItem("$item_hoe");
			if (hoe == null || hoe.m_shared == null || hoe.m_shared.m_attack == null)
			{
				return null;
			}
			return hoe.m_shared.m_attack.m_attackAnimation;
		}
		catch (Exception)
		{
			return null;
		}
	}
}
