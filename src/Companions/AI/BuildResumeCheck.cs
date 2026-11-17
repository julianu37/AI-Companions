using System.Collections.Generic;
using UnityEngine;

namespace Companions;

/// <summary>
/// 16/11/2026 (backlog item 11g, the measured run of the <c>CommHomeBasicBP</c>): <b>a piece the resumed order believes is
/// standing has to be confirmed against the scene</b>. The slab <c>#692</c> of the plan was <c>placed (Direct)</c> and
/// <c>done ... supported</c>, the companion fell 6,8 m and died right after, and after the resume the slab was not in the
/// world - while the order walked on as if it were, and the house closed with one hole that no counter mentioned.
///
/// <para>The saved sets (<c>MHR_Build.Pending.Placed</c>) are an <b>intention</b>, not a fact: they say "this piece was put
/// up", and the world can take a piece back (a fall, a collapse, a body landing on it). <b>What the walk must not do is
/// re-check the whole house</b>: 985 ghost tests right after a respawn is a pause the player would feel, and
/// <c>MHR_Build.FindPlan</c> already treats a batch of 25 x 54 as too expensive. What the player chose (17/11/2026) is the
/// cheap half: <b>confirm the pieces near the body</b> against <c>Piece.GetAllPiecesInRadius</c> - one sweep of the game's
/// own piece list, the same call <c>MHR_Follow</c>/<c>MHR_Mover</c>/<c>CookController</c> already use - and put back in the
/// queue whatever the sweep does not have. The measured orphan fell 6,8 m from the body, so it is inside the radius.</para>
///
/// <para>Pure on purpose (indices, plan poses and scene poses in; the verdict out): the harness pins it in section (av).
/// The Unity side is <c>MHR_Build.RecheckStanding</c>, called from <c>RestoreProgress</c> - the single point both resume
/// paths (death and the gather handover) go through.</para>
/// </summary>
internal static class BuildResumeCheck
{
	/// <summary>
	/// How close a scene piece has to be to a plan pose to be <b>the same piece</b>, in metres. It is the tolerance the
	/// reconciliation uses (<c>tools/reconcile_scene.py</c>): zero would miss every piece the game nudged on placement and
	/// a bigger one would match the neighbour.
	/// </summary>
	internal const float MatchSlack = 0.45f;

	/// <summary>
	/// Fills <paramref name="into"/> with the <paramref name="placed"/> indices whose pose is within
	/// <paramref name="radius"/> (XZ) of <paramref name="center"/> - the only ones the resume is going to confirm against
	/// the scene. Returns how many it added; <paramref name="into"/> is the caller's and is not cleared here.
	///
	/// <para>An index outside the plan, or a pose that is not a real number, is <b>never</b> a suspect: without a pose
	/// there is nothing to look for in the scene, and the piece keeps the treatment of before (trusted). A
	/// <paramref name="radius"/> that is not a number or is zero or less selects <b>nothing</b> - the caller then behaves
	/// exactly as it did before this rule existed.</para>
	/// </summary>
	internal static int Suspects(IEnumerable<int> placed, IList<Vector3> poses, Vector3 center, float radius, ISet<int> into)
	{
		if (placed == null || poses == null || into == null || !Finite(center))
		{
			return 0;
		}
		float limit = Finite(radius) && radius > 0f ? radius : 0f;
		if (limit <= 0f)
		{
			return 0;
		}
		int added = 0;
		foreach (int index in placed)
		{
			if (index < 0 || index >= poses.Count || !Finite(poses[index]))
			{
				continue;
			}
			if (Flat(poses[index], center) <= limit && into.Add(index))
			{
				added++;
			}
		}
		return added;
	}

	/// <summary>
	/// Fills <paramref name="into"/> with the <paramref name="suspects"/> that have <b>no</b> piece of
	/// <paramref name="standing"/> within <see cref="MatchSlack"/> (XZ) - the pieces that are not in the world and must go
	/// back to the queue. Returns how many it added.
	///
	/// <para><b>Fail-open, by design:</b> an empty <paramref name="standing"/> (the world was not ready, the sweep failed)
	/// answers <b>0 and nothing removed</b>. A false "missing" costs a duplicate next to a piece that is already standing;
	/// a false "standing" costs the hole this rule exists to close - and it is the cheaper of the two to be wrong about,
	/// because the walk finds "já está de pé" in one line and pays no material.</para>
	/// </summary>
	internal static int Missing(IEnumerable<int> suspects, IList<Vector3> poses, IList<Vector3> standing, float slack,
		ISet<int> into)
	{
		float limit = Finite(slack) && slack > 0f ? slack : MatchSlack;
		if (suspects == null || poses == null || into == null || standing == null || standing.Count == 0)
		{
			return 0;
		}
		int added = 0;
		foreach (int index in suspects)
		{
			if (index < 0 || index >= poses.Count || !Finite(poses[index]))
			{
				continue;
			}
			bool found = false;
			for (int i = 0; i < standing.Count && !found; i++)
			{
				if (Finite(standing[i]) && Flat(poses[index], standing[i]) <= limit)
				{
					found = true;
				}
			}
			if (!found && into.Add(index))
			{
				added++;
			}
		}
		return added;
	}

	/// <summary>
	/// 16/11/2026 (measured false positive, 22 of 22): <b>the heights a sweep has to cover</b>. The predicate of
	/// <see cref="Suspects"/> is XZ, but <c>Piece.GetAllPiecesInRadius</c> is a <b>sphere</b> - on the measured house (10 m
	/// tall) 132 of the 235 pieces inside the radius horizontally were outside the sphere, so the sweep did not see them
	/// and every one of them looked missing. This answers the centres of the slices that do cover them, the same way
	/// <c>build.delete</c> sweeps a tall build by floors of <c>y</c>.
	///
	/// <para>Fills <paramref name="into"/> with one centre per slice of height <paramref name="height"/>, from the anchor's
	/// own height up to the top of the plan's poses inside <paramref name="radius"/> (XZ) of it. Returns how many it
	/// added; a radius or a slice height of zero or less, or a broken anchor, adds <b>nothing</b> (the caller then does
	/// nothing, which is the fail-open behaviour).</para>
	/// </summary>
	internal static int SliceCenters(IList<Vector3> poses, Vector3 anchor, float radius, float height, IList<Vector3> into)
	{
		if (poses == null || into == null || !Finite(anchor))
		{
			return 0;
		}
		float limit = Finite(radius) && radius > 0f ? radius : 0f;
		float step = Finite(height) && height > 0f ? height : 0f;
		if (limit <= 0f || step <= 0f)
		{
			return 0;
		}
		float top = anchor.y;
		for (int i = 0; i < poses.Count; i++)
		{
			if (Finite(poses[i]) && Flat(poses[i], anchor) <= limit && poses[i].y > top)
			{
				top = poses[i].y;
			}
		}
		int added = 0;
		for (float y = anchor.y; y <= top + step; y += step)
		{
			into.Add(new Vector3(anchor.x, y, anchor.z));
			added++;
		}
		return added;
	}

	/// <summary>
	/// 16/11/2026 (review): the radius a slice's sphere needs so that the whole <paramref name="radius"/> disc is covered
	/// at every height, not only at the slice plane. A sphere centred on the plane reaches only
	/// <c>sqrt(r^2 - dy^2)</c> horizontally at <c>dy</c> below it, so with the sphere radius equal to the disc radius a
	/// piece 5,9 m out at mid-slice is missed (measured geometry: 5,20 m at dy = 3). This answers
	/// <c>sqrt(r^2 + (step/2)^2)</c> - 6,7 m for r = step = 6 m.
	/// </summary>
	internal static float SweepRadius(float radius, float step)
	{
		float r = Finite(radius) && radius > 0f ? radius : 0f;
		float h = Finite(step) && step > 0f ? step / 2f : 0f;
		return (float)System.Math.Sqrt(r * r + h * h);
	}

	/// <summary>Horizontal distance (XZ) between two points - the same rule (and the same guard) as
	/// <see cref="BuildSpread"/>. A point that is not a real number answers the largest float, so it never looks near.</summary>
	private static float Flat(Vector3 a, Vector3 b)
	{
		float dx = a.x - b.x;
		float dz = a.z - b.z;
		float flat = (float)System.Math.Sqrt(dx * dx + dz * dz);
		return float.IsNaN(flat) || float.IsInfinity(flat) ? float.MaxValue : flat;
	}

	/// <summary>Is every coordinate a real number?</summary>
	private static bool Finite(Vector3 v)
	{
		return Finite(v.x) && Finite(v.y) && Finite(v.z);
	}

	/// <summary>Is this float a real number?</summary>
	private static bool Finite(float value)
	{
		return !float.IsNaN(value) && !float.IsInfinity(value);
	}
}
