namespace Companions;

/// <summary>
/// Pendência 12 (the product recipe, 11/11/2026): <b>which endings of the leveling job hand the site back to the
/// build</b> - and what the companion says when it does.
///
/// <para>The handover itself lives in <c>MHR_Level.Finish</c> and needs the game (terrain, hoe, companion, a plan), so
/// the <b>table</b> is the one piece that can be proven outside it. That is exactly how the defect survived until now:
/// the resume set was a list of three keys written inline, and <c>hc_ai_build_too_steep</c> was missing from it - with
/// <c>[Building] LevelGround</c> on by default (the product recipe, never played) a steep site ended the whole order
/// instead of building what fits. A missing key is invisible in the job and only shows up in a run; here it is an
/// assertion.</para>
///
/// <para>Pure (strings only, no Unity, no config): the harness links this file and asserts the table (section (af)).</para>
/// </summary>
internal static class LevelResume
{
	/// <summary>
	/// Does the leveling's ending <paramref name="key"/> hand the site back to the build? <c>true</c> for the three
	/// endings where the ground was actually worked (won, already flat, gave up at the soft level's floor) and for the
	/// <b>refusal</b> (<c>hc_ai_build_too_steep</c>): a site too steep to level is still a site the build can start on -
	/// the pieces that do not fit are skipped and counted, instead of the order ending on the spot.
	/// <c>hc_ai_build_no_hoe</c> stays out on purpose: without a hoe the ground is never touched, and the build that
	/// comes back would ask for the same leveling again and end the same way.
	/// </summary>
	internal static bool Resumes(string key)
	{
		return key == "hc_ai_level_done" || key == "hc_ai_level_already" || key == "hc_ai_level_failed"
			|| key == "hc_ai_build_too_steep";
	}

	/// <summary>
	/// The line the companion logs when the build resumes, or <c>null</c> when it does not. The two "won" endings share
	/// one line; "gave up" and "too steep" say different things on purpose - the player reads the log to know whether
	/// the ground is ready or the house is going up on a slope.
	/// </summary>
	internal static string Message(string key)
	{
		switch (key)
		{
			case "hc_ai_level_done":
			case "hc_ai_level_already":
				return "the ground is ready - back to the build";
			case "hc_ai_level_failed":
				return "the ground is as flat as it gets - back to the build";
			case "hc_ai_build_too_steep":
				return "the ground is too steep to level - back to the build with what it has";
			default:
				return null;
		}
	}
}
