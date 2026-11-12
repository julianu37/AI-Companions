namespace Companions;

/// <summary>
/// P1 (04/11/2026) and 11/11/2026 (Run 10): the <b>body window</b> of the placement check - would a piece at a pose be
/// built <b>through a character</b>?
///
/// <para>The measured bug of 04/11/2026: with the 1 m deck the floor was placed <b>over the head</b> of two companions
/// and they were squeezed to y 37,9 (1 m under the ground, 38,78) and stuck. The original test compared the distance
/// from the piece's <b>point</b> (its centre) to the character's <b>transform</b> (the feet) and needed 0,6 m: a floor
/// 1 m above the feet was 1,0 m away from that point in 3D, so it did <b>not</b> block - while the character is 1,8 m
/// tall and its head went through the floor. Vanilla has no such hole: it tests the ghost's <b>colliders</b> against
/// every character (<c>CheckPlacementGhostVSPlayers</c>), which is the body, not a point.</para>
///
/// <para>Then it became a column: the character was <c>feet - 0,5 m .. feet + 2,0 m</c> and the piece a <b>disc</b> of
/// its largest flat side plus 0,45 m. The disc is what the <b>Run 10</b> (11/11/2026) paid for: a <b>2 m beam</b> became
/// a 1 m disc in every direction, so a companion standing 1,08 m from the beam's pose, <b>1,9 m below it</b>, held the
/// piece for the whole barrier (three short retries), while the game's own ghost check would accept it - the beam's
/// box ends well before him. The window is now the real shape the game uses: the character is a <b>capsule</b> (its
/// own collider: bottom, top and radius) and the piece a <b>rotated box</b> (its three half extents in its own frame),
/// and the answer is the flat distance from the capsule's axis to the box, in the <b>piece's</b> frame.</para>
///
/// <para>Pure on purpose (floats only, no Unity): the window is asserted by the harness (section (s)), so the numbers
/// that decide "a piece may be built here" are visible outside the game. The Unity side - the piece's box at the pose
/// (centre offset by its pivot, AABB of the rotated box through <see cref="HalfY"/>) and the character's capsule - is
/// <c>BuildProbe.BlockedByCharacter</c>.</para>
/// </summary>
internal static class BuildBody
{
	/// <summary>
	/// Is the body's capsule through the piece? <paramref name="localX"/>/<paramref name="localZ"/> are the capsule's
	/// axis minus the piece's centre, <b>in the piece's own frame</b> (the plan's yaw already taken out);
	/// <paramref name="halfX"/>/<paramref name="halfZ"/> are the piece's half extents on that same frame;
	/// <paramref name="pieceBottom"/>/<paramref name="pieceTop"/> its vertical span at the pose (the AABB of the rotated
	/// box) and <paramref name="capsuleBottom"/>/<paramref name="capsuleTop"/>/<paramref name="capsuleRadius"/> the
	/// character's collider.
	///
	/// <para>Two windows, both required: the vertical one (a piece entirely above the head or under the feet never
	/// blocks - the companion walks under a beam; touching exactly does not block either, which is what lets a floor be
	/// placed at the feet of somebody standing on it) and the flat one (the distance from the capsule's axis to the box,
	/// 0 while the axis is over the box - that is why a body <b>beside</b> a wall no longer holds it).</para>
	/// </summary>
	internal static bool Blocks(float localX, float localZ, float halfX, float halfZ, float pieceBottom, float pieceTop,
		float capsuleBottom, float capsuleTop, float capsuleRadius)
	{
		if (pieceTop <= capsuleBottom || pieceBottom >= capsuleTop)
		{
			return false;   // entirely above the head or under the feet: the body and the piece do not cross
		}
		float dx = (localX < 0f ? -localX : localX) - halfX;
		if (dx < 0f)
		{
			dx = 0f;   // the capsule's axis is over the piece: the distance to the box is the vertical one only
		}
		float dz = (localZ < 0f ? -localZ : localZ) - halfZ;
		if (dz < 0f)
		{
			dz = 0f;
		}
		return dx * dx + dz * dz < capsuleRadius * capsuleRadius;
	}

	/// <summary>
	/// 11/11/2026 (Run 10): the vertical half of the piece's box <b>at a pose</b> - the Y half extent of the AABB of a box
	/// with half extents (<paramref name="halfX"/>, <paramref name="halfY"/>, <paramref name="halfZ"/>) turned by a
	/// rotation whose unit axes have these Y components (<paramref name="axisXy"/> for X, <paramref name="axisYy"/> for
	/// Y, <paramref name="axisZy"/> for Z). A 45° beam is <b>0,78 m</b> tall instead of 0,1 m, which is exactly the case
	/// the disc missed. Pure (no Unity) so the harness can assert it; the probe passes the rotated axes' Y.
	/// </summary>
	internal static float HalfY(float halfX, float halfY, float halfZ, float axisXy, float axisYy, float axisZy)
	{
		float y = (axisXy < 0f ? -axisXy : axisXy) * halfX;
		y += (axisYy < 0f ? -axisYy : axisYy) * halfY;
		y += (axisZy < 0f ? -axisZy : axisZy) * halfZ;
		return y;
	}
}
