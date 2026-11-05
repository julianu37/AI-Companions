using System;
using System.Collections.Generic;
using UnityEngine;

namespace Companions;

/// <summary>The pose <see cref="BuildSite"/> chose, with the status of the last point it looked at (so a refusal
/// says why) and how many candidates it tried.</summary>
internal sealed class BuildSiteResult
{
	internal bool Ok;
	internal Vector3 Pos;
	internal Quaternion Rot;
	internal string Status = "no_candidate";
	internal List<string> Reasons = new List<string>();
	internal int Tried;
	internal float Distance;
	/// <summary>How many candidates failed for each reason (so a refusal reads "10 clipping, 2 blocked_by_player").</summary>
	internal Dictionary<string, int> Tally = new Dictionary<string, int>();
}

/// <summary>
/// Fase E2: the <b>placement system</b> - where the piece goes. It is the part no mod in the ecosystem does: the
/// BuildOrders, the PlanBuild and the Buildheim all receive a pose from the player; the companion has to find one.
///
/// <para>It walks candidates out from a <i>hint</i> (the spot the player is looking at): the hint itself first, then
/// rings of <paramref name="radius"/> in 45° steps, and for each candidate it runs the full check:
/// <see cref="BuildProbe.CheckPoint"/> (ground, water, biome, ward, no-build zone, station in range, space, players)
/// plus the <b>ghost clipping</b> test that vanilla does in <c>TestGhostClipping</c> (<c>Player.cs</c> L4176) - the
/// piece's real colliders against everything solid within 10 m, with the 0,2 m tolerance of the game.</para>
///
/// <para><b>Why the ghost:</b> the workbench's "needs space" rule is <c>m_noClipping</c> (measured, see
/// <c>docs/re/building-notes.md</c>), and a piece that may not clip has to be tested with its own colliders. The
/// ghost is built the way vanilla builds its placement ghost: <c>ZNetView.m_forceDisableInit</c> +
/// <c>TerrainOp.m_forceDisableTerrainOps</c> around the instantiate (so no ZDO is created), the joints/rigidbodies
/// removed, everything on the <c>ghost</c> layer - and it is destroyed in the same frame.</para>
///
/// <para>Everything runs on the main thread, once per order (never per frame), and the ghost only exists while
/// <see cref="Check"/> runs.</para>
/// </summary>
internal static class BuildSite
{
	/// <summary>The penetration the game tolerates (vanilla <c>TestGhostClipping(ghost, 0.2f)</c>).</summary>
	internal const float MaxPenetration = 0.2f;

	/// <summary>The reach of the overlap around the ghost (vanilla uses 10 m).</summary>
	private const float OverlapRadius = 10f;

	private static int _ghostLayer = -2;

	private static int GhostLayer()
	{
		if (_ghostLayer == -2)
		{
			_ghostLayer = LayerMask.NameToLayer("ghost");
		}
		return _ghostLayer;
	}

	/// <summary>
	/// Full check of one pose: the point rules of the probe plus the ghost clipping when the piece asks for it
	/// (<c>m_noClipping</c>). Returns the same shape as <see cref="BuildProbe.CheckPoint"/> (with <c>clipping</c>
	/// added), so the two are interchangeable in a log or a test.
	/// </summary>
	internal static Dictionary<string, object> Check(Piece piece, Vector3 pos, Quaternion rot)
	{
		return Check(piece, pos, rot, null);
	}

	/// <summary>The full check with a character to ignore (E6: the companion placing the piece is not an obstacle).</summary>
	internal static Dictionary<string, object> Check(Piece piece, Vector3 pos, Quaternion rot, Component ignore)
	{
		return Check(piece, pos, rot, ignore, true);
	}

	/// <summary>
	/// The same check with the <b>station rule optional</b> (lote 1, 11/11/2026): a blueprint built with
	/// <c>[Building] BlueprintStations</c> on has no workbench planned, so this is the flag that keeps the pose from being
	/// refused with <c>needs_station</c>. Everything else (ground, water, ward, clipping, bodies) is unchanged and the
	/// default keeps every other caller on the vanilla rule.
	/// </summary>
	internal static Dictionary<string, object> Check(Piece piece, Vector3 pos, Quaternion rot, Component ignore, bool requireStation)
	{
		Dictionary<string, object> check = BuildProbe.CheckPoint(piece, pos, rot, ignore, requireStation);
		bool clips = false;
		if (piece != null && piece.m_noClipping)
		{
			clips = Clips(piece, pos, rot);
		}
		check["clipping"] = clips;
		if (clips)
		{
			List<string> reasons = check["reasons"] as List<string>;
			if (reasons == null)
			{
				reasons = new List<string>();
				check["reasons"] = reasons;
			}
			reasons.Add("clipping");
			check["ok"] = false;
			if (Convert.ToString(check["status"]) == "valid")
			{
				check["status"] = "clipping";
			}
		}
		return check;
	}

	/// <summary>
	/// Finds a valid pose near <paramref name="hint"/>. <paramref name="forward"/> orients the piece (the player's
	/// facing); pieces that may rotate get the four 90° yaws tried before the spot is abandoned, which is what a
	/// player does when the bench does not fit. <paramref name="maxCandidates"/> bounds the work.
	/// </summary>
	internal static BuildSiteResult Find(Piece piece, Vector3 hint, Vector3 forward, float radius, int rings, int dirs, int maxCandidates = 48, Component ignore = null, bool requireStation = true)
	{
		BuildSiteResult result = new BuildSiteResult { Pos = hint, Rot = LookRotation(forward), Distance = 0f };
		if (piece == null)
		{
			result.Status = "no_piece";
			return result;
		}
		rings = Mathf.Clamp(rings, 1, 8);
		dirs = Mathf.Clamp(dirs, 4, 24);
		radius = Mathf.Clamp(radius, 1f, 40f);
		Vector3 flatForward = Flat(forward);
		Quaternion baseRot = LookRotation(flatForward);

		List<Vector3> candidates = new List<Vector3> { hint };
		for (int ring = 0; ring < rings; ring++)
		{
			float distance = radius * (ring + 1) / rings;
			for (int dir = 0; dir < dirs; dir++)
			{
				float angle = Mathf.PI * 2f * dir / dirs;
				candidates.Add(hint + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance));
			}
		}

		int tried = 0;
		foreach (Vector3 candidate in candidates)
		{
			if (tried >= maxCandidates)
			{
				break;
			}
			// 04/11/2026 (review R13): the candidate used to be forced onto the terrain. `GetGroundHeight` sees ONLY the
			// terrain layer, so an "adjusted pose" for a piece of the 2nd floor was moved to the GROUND - the measured
			// "log ajustado" that put chests at 39,9 when the plan wanted 43,9. A pose already above the ground is the
			// plan's own y and must stay there; only a pose at/below the ground snaps to it.
			float ground = ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(candidate) : candidate.y;
			float y = candidate.y > ground + 0.1f ? candidate.y : ground;
			Vector3 pos = new Vector3(candidate.x, y, candidate.z);
			float[] yaws = piece.m_canRotate ? new float[] { 0f, 90f, 180f, 270f } : new float[] { 0f };
			foreach (float yaw in yaws)
			{
				tried++;
				Quaternion rot = baseRot * Quaternion.Euler(0f, yaw, 0f);
				Dictionary<string, object> check = Check(piece, pos, rot, ignore, requireStation);
				result.Tried = tried;
				result.Status = Convert.ToString(check["status"]);
				result.Reasons = check["reasons"] as List<string> ?? new List<string>();
				int seen;
				result.Tally.TryGetValue(result.Status, out seen);
				result.Tally[result.Status] = seen + 1;
				if (check["ok"] is bool ok && ok)
				{
					result.Ok = true;
					result.Pos = pos;
					result.Rot = rot;
					result.Distance = Vector3.Distance(new Vector3(hint.x, 0f, hint.z), new Vector3(pos.x, 0f, pos.z));
					return result;
				}
				if (!piece.m_canRotate)
				{
					break;
				}
			}
		}
		result.Pos = hint;
		result.Rot = baseRot;
		return result;
	}

	/// <summary>
	/// The vanilla ghost clipping test: every enabled collider of the piece against everything solid within 10 m.
	/// The ghost is created without a ZDO (the vanilla flags), stripped of what could act on the world and destroyed
	/// in the same frame. Non-convex mesh colliders (the terrain) are not supported by
	/// <c>Physics.ComputePenetration</c>, so the ground itself never counts - which is what the game does too.
	/// </summary>
	private static bool Clips(Piece piece, Vector3 pos, Quaternion rot)
	{
		GameObject ghost = null;
		try
		{
			ghost = CreateGhost(piece, pos, rot);
			if (ghost == null)
			{
				return false;
			}
			Collider[] own = ghost.GetComponentsInChildren<Collider>();
			Collider[] near = Physics.OverlapSphere(pos, OverlapRadius, BuildProbe.PlaceMask());
			foreach (Collider mine in own)
			{
				if (mine == null || !mine.enabled || mine.isTrigger)
				{
					continue;
				}
				foreach (Collider other in near)
				{
					if (other == null || other == mine || !other.enabled || other.isTrigger)
					{
						continue;
					}
					// skip anything that belongs to the ghost itself - INCLUDING the root: IsChildOf(self) is false, so a
					// collider on the prefab's root was being compared with the ghost's own colliders and every candidate
					// "clipped" (measured in the game: 44 of 48 refused with clipping before this fix)
					if (other.gameObject == ghost || other.transform.IsChildOf(ghost.transform))
					{
						continue;
					}
					float distance;
					if (Physics.ComputePenetration(mine, mine.transform.position, mine.transform.rotation, other, other.transform.position, other.transform.rotation, out _, out distance)
						&& distance > MaxPenetration)
					{
						return true;
					}
				}
			}
			return false;
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[Build] Teste de encaixe falhou: " + ex.Message);
			return false;
		}
		finally
		{
			if (ghost != null)
			{
				UnityEngine.Object.Destroy(ghost);
			}
		}
	}

	/// <summary>
	/// The placement ghost as the game builds it (<c>Player.UpdatePlacementGhost</c>, L3488): no ZDO, no terrain ops,
	/// no joints/rigidbodies/force fields/demisters/guide points, every collider on the <c>ghost</c> layer.
	/// </summary>
	private static GameObject CreateGhost(Piece piece, Vector3 pos, Quaternion rot)
	{
		TerrainModifier modifier = piece.GetComponentInChildren<TerrainModifier>();
		bool modifierWasEnabled = modifier != null && modifier.enabled;
		if (modifier != null)
		{
			modifier.enabled = false;
		}
		bool previousOps = TerrainOp.m_forceDisableTerrainOps;
		bool previousInit = ZNetView.m_forceDisableInit;
		TerrainOp.m_forceDisableTerrainOps = true;
		ZNetView.m_forceDisableInit = true;
		GameObject ghost;
		try
		{
			ghost = UnityEngine.Object.Instantiate(piece.gameObject, pos, rot);
		}
		finally
		{
			ZNetView.m_forceDisableInit = previousInit;
			TerrainOp.m_forceDisableTerrainOps = previousOps;
		}
		if (ghost == null)
		{
			return null;
		}
		ItemDrop drop = ghost.GetComponent<ItemDrop>();
		if (drop != null)
		{
			drop.MakePiece();
		}
		if (modifier != null)
		{
			modifier.enabled = modifierWasEnabled;
		}
		Strip<Joint>(ghost);
		Strip<Rigidbody>(ghost);
		Strip<Demister>(ghost);
		Strip<GuidePoint>(ghost);
		Strip<TerrainModifier>(ghost);
		int layer = GhostLayer();
		if (layer >= 0)
		{
			foreach (Transform child in ghost.GetComponentsInChildren<Transform>(true))
			{
				child.gameObject.layer = layer;
			}
		}
		return ghost;
	}

	private static void Strip<T>(GameObject root) where T : Component
	{
		foreach (T component in root.GetComponentsInChildren<T>(true))
		{
			if (component != null)
			{
				UnityEngine.Object.Destroy(component);
			}
		}
	}

	private static Vector3 Flat(Vector3 direction)
	{
		direction.y = 0f;
		if (direction.sqrMagnitude < 0.01f)
		{
			return Vector3.forward;
		}
		return direction.normalized;
	}

	private static Quaternion LookRotation(Vector3 forward)
	{
		return Quaternion.LookRotation(Flat(forward), Vector3.up);
	}
}
