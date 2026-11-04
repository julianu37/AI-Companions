namespace Companions;

/// <summary>
/// P1 (04/11/2026, player's report: "dois companions travados embaixo do piso"): the <b>body window</b> of the placement
/// check - would a piece at a pose be built <b>through a character</b>?
///
/// <para>The measured bug: with the 1 m deck the floor was placed <b>over the head</b> of two companions and they were
/// squeezed to y 37,9 (1 m under the ground, 38,78) and stuck. The old test compared the distance from the piece's
/// <b>point</b> (its centre) to the character's <b>transform</b> (the feet) and needed 0,6 m: a floor 1 m above the feet
/// was 1,0 m away from that point in 3D, so it did <b>not</b> block - while the character is 1,8 m tall and its head went
/// through the floor. Vanilla has no such hole: it tests the ghost's <b>colliders</b> against every character
/// (<c>CheckPlacementGhostVSPlayers</c>), which is the body, not a point.</para>
///
/// <para>So the body is a <b>column</b>: from <see cref="Below"/> under the feet (the step / a cow standing half in the
/// ground) to <see cref="Above"/> over them (the head plus room for the step), and the piece is a <b>box</b>: its flat
/// radius (<c>max(size.x, size.z) / 2</c>) plus the body's own radius (<see cref="Radius"/>) decides in XZ, and its
/// vertical span decides in Y. A piece entirely above the head does not block (the companion can walk under a beam), and a
/// piece beside the body does not block either.</para>
///
/// <para>Pure on purpose (floats only, no Unity): the window is asserted by the harness (section (s)), so the numbers that
/// decide "a piece may be built here" are visible outside the game.</para>
/// </summary>
internal static class BuildBody
{
	/// <summary>The body's own radius in XZ (a Valheim character's capsule is ~0,3 m; 0,45 leaves room for the shoulders
	/// and the arm swing). The piece adds its own flat radius on top of this.</summary>
	internal const float Radius = 0.45f;

	/// <summary>How far under the feet a piece still counts as through the body: the step, a slope, a character standing
	/// half in the ground (the measured case had the companion 1 m down).</summary>
	internal const float Below = 0.5f;

	/// <summary>How far over the feet the body reaches: a Valheim character is ~1,8 m tall; 2,0 adds the step.</summary>
	internal const float Above = 2f;

	/// <summary>
	/// The XZ spacing the body test needs for a piece: the piece's own flat radius (<paramref name="flat"/>, half of its
	/// largest horizontal side) plus the body's <see cref="Radius"/>, and never less than the probe's own
	/// <paramref name="minSpacing"/> (0,6 m - a spot right under a character always blocks).
	/// </summary>
	internal static float Spacing(float flat, float minSpacing)
	{
		float spacing = flat + Radius;
		return spacing < minSpacing ? minSpacing : spacing;
	}

	/// <summary>
	/// Is the piece through the body? <paramref name="dx"/>/<paramref name="dz"/> are the body's feet minus the piece's
	/// point (XZ only), <paramref name="pieceBottom"/>/<paramref name="pieceTop"/> the piece's vertical span at the pose,
	/// <paramref name="feetY"/> the character's feet and <paramref name="spacing"/> the radius from
	/// <see cref="Spacing"/>. Both windows have to hold: side by side (or above the head) never blocks.
	/// </summary>
	internal static bool Blocks(float dx, float dz, float pieceBottom, float pieceTop, float feetY, float spacing)
	{
		if (dx * dx + dz * dz >= spacing * spacing)
		{
			return false;   // beside the piece: the body and the piece do not touch
		}
		return pieceTop > feetY - Below && pieceBottom < feetY + Above;
	}

	/// <summary>Convenience on top of <see cref="Blocks"/>: the same test from world-space numbers (kept pure: no Vector3).</summary>
	internal static bool Blocked(float feetX, float feetY, float feetZ, float pointX, float pointY, float pointZ,
		float halfHeight, float spacing)
	{
		return Blocks(feetX - pointX, feetZ - pointZ, pointY - halfHeight, pointY + halfHeight, feetY, spacing);
	}
}
