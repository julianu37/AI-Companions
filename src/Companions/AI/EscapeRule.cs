using System.Collections.Generic;
using UnityEngine;

namespace Companions;

/// <summary>
/// 12/11/2026 (measured: two companions frozen inside the site they were building, the log repeating the same three
/// detour points and the same two goals for over 20 minutes): <b>how the mover stops cycling on the same answer</b>.
///
/// <para>The livelock, read from the log:</para>
/// <code>
/// [Move|Follow] couldn't get to (-165,0, 39,5, -399,7) from (-153,1, 38,8, -416,7) - giving up
/// [Move|Follow] the way is blocked - trying another one at (-152,5, 38,8, -413,8) (0,0m up)
/// </code>
/// <para>Two halves of the same defect. <c>TryDetour</c> scored 48 probes around the body and always picked the same
/// best one, which lay <b>inside</b> the enclosure; after three tries <c>Unstick</c> gave up, and <c>Reset</c> zeroed
/// <c>DetourTries</c> on a fixed 15 s clock - so the same three points came back, forever. The companion spent every
/// frame of twenty minutes re-deciding the same non-answer.</para>
///
/// <para>The rule kept: a spot that was already tried is not an alternative, and a give-up that came from the same
/// place waits longer every time. Neither changes what the mover does on the <b>first</b> attempt - the three tries the
/// normal stuck ladder always had stay exactly as they were.</para>
///
/// <para>Pure (numbers and vectors only, no <c>Physics</c>, no <c>Time</c>): the harness links this file and asserts the
/// table (section (ai)).</para>
/// </summary>
internal static class EscapeRule
{
	/// <summary>How close a candidate must be to a spot already tried to count as the same spot, in meters. One body
    /// width: two probes 1,5 m apart are not different escape routes, they are the same square of floor.</summary>
	internal const float SameSpot = 1.5f;

	/// <summary>How far the body has to move for the wait to start over, in meters. Walking out of the pocket is the
    /// exit; standing 2 m to the side inside it is not a different problem.</summary>
	internal const float MovedAway = 6f;

	/// <summary>
	/// How long the mover waits after the <paramref name="giveUps"/>-th give-up <b>from the same place</b>: 15 s the
	/// first time, then 30, 60, and a 120 s ceiling. A fixed 15 s is what turned the trap into a livelock - it kept
	/// buying the same three detours again.
	/// </summary>
	internal static float RetryWait(int giveUps)
	{
		if (giveUps <= 1) return 15f;
		if (giveUps == 2) return 30f;
		if (giveUps == 3) return 60f;
		return 120f;
	}

	/// <summary>
	/// The same wait, with one exception: when the goal itself keeps moving (<paramref name="goalMoving"/>, a player
	/// walking on with the companion following) the wait stays at the 15 s it always had. The player is a possible exit
	/// the companion cannot see from inside - they can come back, open the door, break the piece - so a companion that
	/// stopped following for two minutes would be a worse bug than the one being fixed. The growing wait is for a goal
	/// that stays put: a job, a build piece, a spot on the map.
	/// </summary>
	internal static float RetryWait(int giveUps, bool goalMoving)
	{
		return goalMoving ? 15f : RetryWait(giveUps);
	}

	/// <summary>Has this spot (or one within <paramref name="radius"/>) already been used as a detour?</summary>
	internal static bool AlreadyTried(List<Vector3> tried, Vector3 p, float radius)
	{
		for (int i = 0; i < tried.Count; i++)
		{
			Vector3 d = tried[i] - p; d.y = 0f;
			if (d.sqrMagnitude <= radius * radius) return true;
		}
		return false;
	}
}
