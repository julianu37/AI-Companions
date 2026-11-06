namespace Companions;

/// <summary>
/// 05/11/2026 (the clearance of the body, measured in the game): <b>does the body fit here?</b> and <b>may it walk in?</b>
///
/// <para>The measured defect (<c>docs/re/deck-clearance.md</c>, the print of the player): the stone cottage finished
/// <c>peças: 64/64, 0 pulada(s)</c> and the companion ended <b>0,88 m inside the terrain</b> (feet 37,90 against a
/// ground of 38,78), squeezed under the deck's floor (1,00 m of gap for a 1,80 m body). The game has no head room test
/// anywhere in the movement (<c>CheckCapsule</c>/<c>OverlapCapsule</c> appear once in the whole assembly, in
/// <c>Fireplace</c>), no crouch that shortens the collider, a depenetration capped at <b>2 m/s</b> and its own rescue
/// fires at <b>1,00 m</b> under the ground - he was at 0,88 m and 12 cm were missing. He only left when the player
/// demolished the floor.</para>
///
/// <para>So the rule is ours, and it has <b>two</b> halves that must not be confused:
/// <see cref="MayEnter"/> refuses the step only when the <b>destination</b> does not fit <b>and</b> the place the body is
/// in right now does fit. A body that is already inside a space that does not fit keeps <b>every</b> direction open -
/// without that half the companion becomes a statue in the gap and the player has to demolish the piece (which is
/// exactly what happened).</para>
///
/// <para>Pure on purpose (floats and booleans, no Unity, no <c>Physics</c>): the geometry that decides "may I enter" is
/// asserted by the harness (section (ae)), so the numbers are visible outside the game. The Unity side - the capsule of
/// the real body (<c>BuildProbe.CapsuleOf</c>, never a constant: the Dverger prefabs may have another body), the
/// <c>Physics</c> query and the exclusion of the bodies - is <c>MHR_Mover.FitsBody</c>.</para>
/// </summary>
internal static class MHR_Clearance
{
	/// <summary>
	/// The internal slack of the test, in meters: the spheres are pushed <b>1 cm</b> up from the feet and their radius
	/// shrinks by the same centimeter, so the surface the body stands on is never what refuses the step (the same kind of
	/// margin <c>MHR_Mover.SafeStep</c> already uses: a sphere of 0,20 inside a body of 0,30).
	/// </summary>
	internal const float Slack = 0.01f;

	/// <summary>
	/// The band above the feet inside which a collider is still "something I stand on" and not a ceiling, in meters. It only
	/// absorbs the floor the body is on poking a few centimeters above <c>transform.position</c> (the measured feet are
	/// exactly the ground) and an uneven terrain - <b>not</b> a step: a step's collider reaches the ground, so its
	/// <b>bottom</b> is below the feet and it is never a ceiling whatever this band says (see <see cref="Blocks"/>).
	/// </summary>
	internal const float StepIgnore = 0.25f;

	/// <summary>
	/// How far <b>under</b> the terrain the body has to be for the rescue (and the <c>enterrado</c> count of the order) to
	/// fire, in meters. The margin absorbs an uneven terrain and a step; the measured case was <b>0,88 m</b> and the game's
	/// own limit is 1,00 m, so anything above this is never a false alarm on a sandbank.
	/// </summary>
	internal const float BuriedDepth = 0.4f;

	/// <summary>
	/// How far ahead of the feet the candidate point is sampled, in meters. The body walks ~0,08 m per frame and its radius
	/// is 0,30, so 1,20 m stops it with ~0,9 m of margin <b>before</b> touching the slab - and it is short enough not to
	/// refuse a doorway the body still has to walk through.
	/// </summary>
	internal const float LookAhead = 1.2f;

	/// <summary>
	/// The lateral deviations tried before giving the step up, in degrees: the smallest first, so the companion stays as
	/// close to its route as the space allows, then the wider ones to walk round the edge of a deck. There is no zero in the
	/// table - that is the direction that was just refused.
	/// </summary>
	internal static readonly float[] Deviations = { 35f, -35f, 70f, -70f, 90f, -90f };

	/// <summary>
	/// The whole rule, and the one line the player asked for: <b>refuse the step only when the destination does not fit
	/// and the place the body is in does fit</b> - <c>fitsDest || !fitsHere</c>. A body already inside a space that does
	/// not fit (<c>fitsHere == false</c>) may move anywhere, including deeper: what must stay possible is the way out.
	/// </summary>
	internal static bool MayEnter(bool fitsDest, bool fitsHere)
	{
		return fitsDest || !fitsHere;
	}

	/// <summary>
	/// The <b>centers of the two spheres</b> of the body's capsule when it stands with its feet at
	/// <paramref name="feetY"/>, from the capsule it really has: <paramref name="bottom"/>/<paramref name="top"/> are the
	/// ends of its AABB in world space and <paramref name="radius"/> the radius (all three from
	/// <c>BuildProbe.CapsuleOf</c>, which reads the <c>CapsuleCollider</c> of the prefab - the vanilla body is 1,80 m and
	/// r 0,30, and nothing in the game changes it: there is no crouch).
	///
	/// <para><paramref name="bottom"/> is <b>not</b> assumed to be <c>transform.position.y</c>: the lift between them is
	/// measured here and applied to <paramref name="feetY"/> (the transform is the feet, the collider's bottom is a prefab
	/// number). Only the <b>shape</b> is taken from the measurement, so this is exact for a standing capsule - the case of
	/// every walking <c>Character</c> - and a body lying down (dead, asleep) would give a wider AABB; refusing a step over an
	/// overestimated body is the safe side and the callers skip the dead. A capsule shorter than two radii collapses to
	/// <b>one</b> sphere (<c>CheckCapsule</c> with the ends crossed is undefined), and a body that cannot be measured answers
	/// <b>false</b> - never a refusal out of a bad measurement.</para>
	/// </summary>
	internal static bool Spheres(float bottom, float top, float radius, float feetY, out float lowerY, out float upperY,
		out float testRadius)
	{
		lowerY = feetY;
		upperY = feetY;
		testRadius = radius;
		float height = top - bottom;
		if (height < 0.1f || radius < 0.05f)
		{
			return false;
		}
		float lift = bottom - feetY;
		testRadius = radius - Slack;
		lowerY = feetY + lift + radius + Slack;
		float upper = feetY + lift + height - radius;
		upperY = upper > lowerY ? upper : lowerY;
		return true;
	}

	/// <summary>
	/// Is <b>this</b> collider, found inside the body's capsule, an obstacle? It is, unless one of these:
	///
	/// <para><paramref name="isTerrain"/> - a heightfield has no overhangs, so it can never be the ceiling of a standing
	/// body, and its collider spans the whole zone (its top is a mountain, not the ground at the feet), which is why it
	/// cannot be judged by a height at all;</para>
	///
	/// <para><paramref name="isCharacter"/> - the bodies are the game's own business (<c>Player.CheckPlacementGhostVSPlayers</c>
	/// and the depenetration), and counting them would make the companion refuse to walk past its friends;</para>
	///
	/// <para>and <paramref name="colliderBottomY"/> below <paramref name="floorY"/> - <b>the load-bearing distinction of
	/// the whole rule</b>. A <b>ceiling</b> <i>floats</i>: the deck's floor at 1,00 m has its underside 0,89 m above the
	/// feet, inside the body. A <b>step</b>, a <b>wall</b> or the floor the body stands on <b>does not</b>: it reaches the
	/// ground, so its bottom is at (or under) the feet and the capsule overlapping it is the normal case that the horizontal
	/// checks already handle. The <b>top</b> of the collider would be the wrong key: a stair (bottom on the ground) has its
	/// top a meter up, so it would read as a ceiling and the companion would freeze at the foot of every stair.</para>
	/// </summary>
	internal static bool Blocks(float colliderBottomY, bool isTerrain, bool isCharacter, float floorY)
	{
		if (isTerrain || isCharacter)
		{
			return false;
		}
		return colliderBottomY > floorY;
	}

	/// <summary>
	/// Is the body <b>under the terrain</b> by more than <see cref="BuriedDepth"/>? It is the predicate of both the rescue
	/// (<c>MHR_Mover.RescueSunk</c>) and the <c>enterrado</c> count of the order's closing line
	/// (<c>MHR_Build.Finish</c>) - one rule, never a second copy with another margin.
	/// </summary>
	internal static bool IsBuried(float depth)
	{
		return depth > BuriedDepth;
	}
}
