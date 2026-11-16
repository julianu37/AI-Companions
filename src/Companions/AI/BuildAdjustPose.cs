namespace Companions;

/// <summary>
/// 16/11/2026 (the fatal fall of three measured runs, all on the same edge of the top storey): <b>an adjusted pose may
/// move a piece sideways, never up or down a storey</b>.
///
/// <para>The defect it closes is a <b>check that only looked at two of the three axes</b>. <c>MHR_Build.AdjustPose</c>
/// validated the moved pose with the plan piece's <b>structural radius</b> - a horizontal cap - and nothing else:</para>
///
/// <code>
/// float dx = site.Pos.x - state.Pos.x;
/// float dz = site.Pos.z - state.Pos.z;
/// if (dx * dx + dz * dz &gt; radius * radius) return false;   // metres, and only XZ
/// </code>
///
/// <para>When the companion fell from the top storey (<c>y = 44,9</c>) to the ground floor (<c>y = 38,8</c>) looking for a
/// pose after the fall, <c>BuildSite.Find</c> answered with <b>the same x and z, one storey down</b> - the piece sits on
/// the ground floor, it is free, it is inside 1 m horizontally - and the check above <b>passed</b>, because
/// <c>dx² + dz² = 0</c>. The log of 16/11/2026 says exactly that, and it is the whole mechanism:</para>
///
/// <code>
/// log ajustado: piece_chest_wood de (-156,6, 44,9, -421,6) para (-156,6, 38,8, -421,6) - falls
/// </code>
///
/// <para>Same x, same z, <b>6,1 m down</b>. That single line is both defects of the fall: the chest of the upper floor is
/// placed on the ground floor (<c>reconcile_scene</c> counts it as the one piece out of place, on every run), and the
/// companion, sent to walk to it, falls the same 6,1 m. <b>The adjustment must stay in the piece's own storey.</b></para>
///
/// <para>Pure on purpose (numbers in, a verdict out): the harness pins the table (section (au)) and
/// <c>MHR_Build.AdjustPose</c> adds only the search.</para>
/// </summary>
internal static class BuildAdjustPose
{
	/// <summary>
	/// How much (metres) an adjusted pose may differ in <b>height</b> from the plan's pose. It matches
	/// <see cref="BuildStoreyGate.SameStorey"/> (1 cm): the blueprint parser rounds its key to 1 cm, so a pose that moved
	/// more than that up or down is on another storey and is a different piece of the house, not the same one nudged
	/// aside. The measured defect moved 6,1 m.
	/// </summary>
	internal const float SameStorey = 0.01f;

	/// <summary>
	/// May the pose at (<paramref name="posY"/>) replace a plan pose at (<paramref name="planY"/>) - and does it stay inside
	/// the <b>horizontal</b> radius the piece is allowed to be nudged by?
	///
	/// <para>The horizontal rule is the caller's (a wall/roof gets 1 m, furniture a wider ring) and is passed in
	/// <paramref name="radius"/>; the vertical rule is this class's, because a piece moved a storey is not "the same piece
	/// a metre to the side". Both are needed: the measured bug passed the horizontal one <b>perfectly</b> (zero metres).</para>
	///
	/// <para>A coordinate that is not a number never moves anything.</para>
	/// </summary>
	internal static bool MayMove(float posY, float planY, float dx, float dz, float radius)
	{
		if (float.IsNaN(posY) || float.IsInfinity(posY) || float.IsNaN(planY) || float.IsInfinity(planY)
			|| float.IsNaN(dx) || float.IsInfinity(dx) || float.IsNaN(dz) || float.IsInfinity(dz)
			|| float.IsNaN(radius) || float.IsInfinity(radius) || radius < 0f)
		{
			return false;
		}
		// the storey the piece belongs to: moving it up or down is a different piece of the house
		float up = posY - planY;
		if (up > SameStorey || up < -SameStorey)
		{
			return false;
		}
		return dx * dx + dz * dz <= radius * radius;
	}
}
