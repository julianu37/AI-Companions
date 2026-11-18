using UnityEngine;

namespace Companions;

/// <summary>
/// 17/11/2026 (the bungalow blueprint, measured: <b>8.046</b> <c>retry later: piece_chest_wood ... <b>1/3</b></c> lines
/// for the same chest, the counter never reaching 3): <b>a piece whose pose was adjusted is still the same piece</b>.
///
/// <para>The defect was an identity test by <b>pose</b>. <c>MHR_Build.NudgePiece</c> gives a piece refused by the spot one
/// chance at the closest pose the game accepts - up to <b>5 m</b> for furniture, <b>1 m</b> for structural - and it moves
/// <c>State.Pos</c>/<c>State.Rot</c>, never the plan's step and never the index. The retry queue then compared the plan's
/// pose with the worker's working pose, did not match, made a <b>fresh</b> step object on every attempt, and the attempt
/// counter restarted at 1 for ever: the 3-attempt cap could never be reached, so the piece was never given up and the
/// order never moved past it.</para>
///
/// <para>The identity is the <b>plan index</b> - the same rule <c>PieceIndex</c> already uses for the counters ("the copy
/// at the end is not another piece"). The pose is the fallback only where there is no index: a single piece, a template, an
/// index that the plan no longer has after a remap.</para>
///
/// <para>Pure on purpose (numbers and vectors in, a verdict out): the harness pins it in section (aw). The Unity side is
/// <c>MHR_Build.SamePiece</c>.</para>
/// </summary>
internal static class BuildRetryKey
{
	/// <summary>
	/// Is the step the worker is on the same piece as the plan's step at <paramref name="planIndex"/>?
	/// <list type="bullet">
	/// <item>Both indices known: they are the same piece when they are <b>equal</b>, whatever the poses say - the pose of a
	/// nudged piece is not the plan's.</item>
	/// <item>Only one index known (a plan that was remapped, a piece that is not in it): <b>never</b> the same - guessing by
	/// pose across a remap would merge two different pieces, and the cost of a wrong merge is a piece that is never
	/// built.</item>
	/// <item>Neither index (a single piece, a template): the pose decides, with <paramref name="poseSlack"/> metres of
	/// slack for the float of a placement.</item>
	/// </list>
	/// </summary>
	internal static bool Same(int planIndex, int stateIndex, Vector3 planPos, Vector3 statePos, float poseSlack)
	{
		if (planIndex >= 0 && stateIndex >= 0)
		{
			return planIndex == stateIndex;
		}
		if (planIndex >= 0 || stateIndex >= 0)
		{
			return false;
		}
		float slack = poseSlack > 0f ? poseSlack : 0f;
		return (planPos - statePos).sqrMagnitude < slack * slack + 0.0001f;
	}
}
