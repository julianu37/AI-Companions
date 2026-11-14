namespace Companions;

/// <summary>
/// 14/11/2026 (the solo run of the <c>CommHomeBasicBP</c>, measured): <b>is the companion standing where it has to place
/// the piece, or is it one storey off?</b>
///
/// <para>The mover's arrival test (<c>MHR_Mover.Go</c>) answers "within <c>dist</c> flat and within 1,5 m vertically". The
/// vertical tolerance was a <b>step</b>'s: a floor half a metre up, a slope, a body on the same floor. This house has
/// <b>1,0 m</b> of crawlspace between its ground floor (38,8) and its deck (39,8), so the body standing <b>under</b> the
/// deck it was walking to was, by that test, <b>arrived</b>: it stopped, the build's clock ran 12 s without getting closer
/// and the piece went to the retry queue. Measured in the run: <b>11 of the 15</b> <c>no progress</c> lines had the body at
/// 38,8/38,7 and the walk spot at 39,8/39,9 - <c>dy</c> of <b>1,0/1,1 m</b> and <c>flat</c> between 0,6 and 2,5 m. The run
/// that worked (12/11) logged the other path: <c>1,1m below where it needs to be - jumping up onto (...)</c>.</para>
///
/// <para>The comment on that test already said what it was for ("not standing under it on the floor below"); the number
/// was too loose to keep the promise. The fix is the tolerance, not the test: a walk whose target is a <b>surface to stand
/// on</b> is arrived at half a metre, a walk to anything else keeps the 1,5 m step.</para>
///
/// <para>Pure on purpose (plain floats, no <c>UnityEngine</c>): the harness asserts the table (section (ao)) outside the
/// game. The Unity side is <c>MHR_Mover.SurfaceArrival</c>/<c>ArrivalLevel</c> and the call from
/// <c>MHR_Build.Goto</c>.</para>
/// </summary>
internal static class BuildArrival
{
	/// <summary>The vertical tolerance for a walk whose target is a <b>surface the companion stands on</b>: half a metre
	/// (a threshold, a terrain bump, the top of a stair). Deliberately below half the measured 1,0 m crawlspace, so
	/// neither the 0 m of a piece at the feet nor the 1,0 m of this house sits on the boundary.</summary>
	internal const float Surface = 0.5f;

	/// <summary>The historical vertical tolerance, for a walk that is not about standing on a particular surface: 1,5 m
	/// (a step, a slope, a body on the same floor).</summary>
	internal const float Step = 1.5f;

	/// <summary>
	/// 14/11/2026 (the same run, measured): the <b>flat</b> tolerance for a walk to a surface. <c>Reach</c> (2,5 m) is the
	/// radius of a <b>placement</b> - "can the piece be placed from here" - and using it as "have I got there" stopped the
	/// body <b>2,19 m</b> short of the walk spot, on the ground <b>under</b> the piece (pieces 147/148). A step's worth of
	/// slack is enough for the navmesh's own edge and keeps the body on the surface the walk was chosen for.
	/// </summary>
	internal const float SurfaceReach = 1.0f;

	/// <summary>
	/// Is a body whose feet are <paramref name="feetY"/> standing at a target whose surface is <paramref name="targetY"/>?
	/// <paramref name="surface"/> says whether the target is a surface the companion has to stand <b>on</b> (the build's walk
	/// spot) or merely somewhere to be near (a piece at the feet, a destination). The two ends of the measured run are the
	/// cases below: 1,0 m under a deck is <b>not</b> arrived when the deck is where the piece is placed from, and the same
	/// 1,0 m under a piece it is merely walking to is not a question this rule answers - it keeps the old tolerance.
	/// </summary>
	internal static bool Arrived(float feetY, float targetY, bool surface)	{
		float dy = targetY - feetY;
		if (dy < 0f)
		{
			dy = -dy;
		}
		return dy <= (surface ? Surface : Step);
	}
}
