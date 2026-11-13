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
	/// How long a body may stay trapped before the emergency is allowed: stuck, every detour already tried, no step up,
	/// and still in the same place after a minute and a half. Measured runs had the companions frozen for over <b>twenty
	/// minutes</b>, so 90 s is already very generous - it leaves the whole stuck ladder, the climb and all three detours
	/// more than enough time to succeed on their own.
	/// </summary>
	internal const float RescueAfter = 90f;

	/// <summary>How far the last good spot is kept up to date: only when the body has really moved (<b>1 m</b>). The
    /// position was recorded every frame the route was full, so "the last place a full route started from" was the
    /// body's own feet - teleporting there is teleporting nowhere.</summary>
	internal const float GoodMoved = 1f;

	/// <summary>How far the body has to move for it to count as "not stuck" (<b>0,5 m</b>): the clock behind
    /// <c>stuckFor</c> and behind the emergency. Below this a body shifting on its feet is still the same body in the
    /// same place.</summary>
	internal const float StuckMove = 0.5f;

	/// <summary>
	/// 13/11/2026 (measured live: a companion teleported into the pit the run left behind at (-163,4, 37,9, -429,0)
    /// patrolled <b>1 m laps for 40 s without ever rising above y=37,9</b>, and <c>stuckFor</c> read 0,0 / 4,8 / 9,8 /
    /// 4,2 / 0,5 / 2,7 / 7,7 / 12,7 - it never reached the 90 s of <see cref="RescueAfter"/>): the "has not moved" clock
    /// was <b>any 0,5 m of travel</b>, so pacing in a circle inside a pocket reset it for ever. The emergency could not
    /// fire in the one situation it exists for.
    ///
    /// <para>The fix is not a longer clock - a body that paces faster would still reset it. It is a <b>pocket</b>: the
    /// clock only restarts when the body leaves the circle it was in, not when it shuffles within it. A body that is
    /// really walking away covers more than <paramref name="pocketRadius"/> and resets honestly; a body going round and
    /// round stays inside and the clock runs.</para>
	/// </summary>
	internal const float PocketRadius = 2f;

	/// <summary>
	/// Does a body that stands at <paramref name="pos"/> still count as being in the pocket it was recorded at
    /// (<paramref name="anchor"/>)? True means <b>do not reset</b> the trapped clock. The radius is the same one
    /// <see cref="GoodProbe"/> uses for "this spot is an exit rather than a shuffle", so the two agree on what a
    /// different place is.
	/// </summary>
	internal static bool InPocket(Vector3 anchor, Vector3 pos)
	{
		Vector3 d = pos - anchor;
		d.y = 0f;   // pacing on a slope must not read as leaving the pocket
		return d.sqrMagnitude <= PocketRadius * PocketRadius;
	}

	/// <summary>
	/// 13/11/2026 (the pit): the three clocks a trapped body is judged by, in one place.
	///
	/// <para><paramref name="stuckFor"/> is how long it has been inside the pocket it was recorded in;
	/// <paramref name="noCloser"/> is how long it has failed to get nearer the goal; and
	/// <paramref name="trappedFor"/> is how long it has been trapped without ever getting anywhere. Any one of them is
	/// enough for the emergency.</para>
	///
	/// <para>Measured live: the companion that could not leave the pit at (-163,4, 37,9, -429,0) was asked by this
	/// rule <b>439 times</b> over the rest of the run and never reached 90 s on either clock, while it placed 326 more
	/// pieces from the bottom of the hole. The third clock is only closed by real progress towards the goal (a rebuild
	/// of <c>Remaining</c>, 1 m closer) or by the rescue itself, so a body being handed a new answer to the same trap
	/// keeps counting.</para>
	///
	/// <para>The other two guards the emergency has always had are here too, for the same reason the clocks are: the
	/// caller must answer this BEFORE the 48-raycast probe. Swimming and fighting answer "no" to all of it - a teleport
	/// there is a way to lose a fight or to land in deep water.</para>
	/// </summary>
	internal static bool Trapped(float stuckFor, float noCloser, float trappedFor, bool swimming, bool inCombat)
	{
		if (swimming || inCombat) return false;
		return stuckFor >= RescueAfter || noCloser >= RescueAfter || trappedFor >= RescueAfter;
	}

	/// <summary>Rings of the emergency probe around a trapped body, in meters. The first ring clears a pocket of a body's
    /// width (the measured one was 2-3 m across); the last one reaches past a small room.</summary>
	internal static readonly float[] ProbeRings = { 3f, 5.5f, 8f };

	/// <summary>
	/// 13/11/2026 (the pit): how long to wait before searching again after a search that found <b>no</b> spot. The probe
	/// is 48 raycasts plus a navmesh path, and the trapped clock is no longer restarted by anything that is trying to
	/// help (that is the point of <see cref="Trapped"/>), so a body walled in with nowhere to walk to would otherwise
	/// search every frame for the rest of the order - the run of 13/11 had exactly that body in the pit, asked about
	/// <b>439 times</b>. Two seconds is short enough that a spot which opens up (a piece finished, a wall broken) is
	/// found quickly, and long enough to keep the cost off the frame.
	/// </summary>
	internal const float ProbeWait = 2f;

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

	/// <summary>
	/// May the emergency move happen? All of it has to hold, in this order: the body has been in the SAME place for
	/// <see cref="RescueAfter"/> seconds (<paramref name="stuckFor"/>); it is not swimming and not fighting, because a
	/// teleport there is a way to lose a fight or to land in deep water; and there is a last good spot to go back to.
	/// </summary>
	internal static bool CanRescue(float stuckFor, bool swimming, bool inCombat, bool hasGoodSpot)
	{
		return hasGoodSpot && TrappedEnough(stuckFor, swimming, inCombat);
	}

	/// <summary>
	/// The half of <see cref="CanRescue"/> that costs nothing to ask: the body has not moved for
	/// <see cref="RescueAfter"/> seconds, and it is neither swimming nor fighting. Split out because the caller has to
	/// answer this BEFORE probing for a spot - the probe is 48 raycasts and a navmesh path, and doing it every frame
	/// against a body that is simply waiting for a door would be worse than the bug it fixes.
	/// </summary>
	internal static bool TrappedEnough(float stuckFor, bool swimming, bool inCombat)
	{
		if (swimming || inCombat) return false;
		return stuckFor >= RescueAfter;
	}

	/// <summary>
	/// Is this probe worth moving to? A spot must be on the ground layer (the raycast found it), clear of deep water,
	/// and far enough from where the body stands that the move is an exit rather than a shuffle (<b>2 m</b>).
	/// </summary>
	internal static bool GoodProbe(float distance, float waterDepth)
	{
		return distance >= 2f && waterDepth < 0.5f;
	}

	/// <summary>
	/// Has the last good spot changed enough to be worth recording? Only when the body moved more than
	/// <see cref="GoodMoved"/>: with the position rewritten every frame the "last good spot" is the body's own feet.
	/// </summary>
	internal static bool WorthRecording(Vector3 good, Vector3 pos)
	{
		return Vector3.Distance(good, pos) > GoodMoved;
	}
}
