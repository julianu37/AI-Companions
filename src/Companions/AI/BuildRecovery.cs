using System;
using System.Collections.Generic;

namespace Companions;

/// <summary>
/// 13/11/2026 (the solo run of <c>CommHomeBasicBP</c>, measured: <b>983/985</b> and <c>hc_ai_blueprint_done</c>; the player's
/// rule over the screenshot: <i>"peças faltando na construção não será tolerado"</i>): <b>a piece the order gave up comes back
/// to the plan</b>, on a ladder of three places - <b>after a few other pieces</b>, <b>at the end of its storey</b>, and only
/// <b>last</b> at the end of the whole construction (the player's words: <i>"entra na fila pra uma nova tentativa depois de
/// umas outras 5 ~10 peças colocadas e depois no fim das peças do andar da blueprint e só por ultimo no final de toda
/// construção"</i>).
///
/// <para>The defect it fixes is not this blueprint's, it is the plan's own shape: <c>MHR_Build.Advance</c> is a
/// <b>forward-only</b> scan (<c>next = state.Index + 1</c>), and the only way out of <c>SkippedIndices</c> was a piece that was
/// already <b>placed</b> (<c>CountPlaced</c>). So a piece that spent its three attempts was out of the plan <b>for good</b> —
/// even when the reason it failed stopped being true two hundred pieces later. The two floors of that run are the case: they
/// failed with <c>no progress</c> (the body was one storey below, <c>route partial</c>, the way up not built yet), and the
/// house that finished around them is exactly what would have held them — the player read it from the screenshot before the
/// log was read: <i>"aqueles dois ali que faltaram não iriam cair se ele tivesse colocado, pois estariam ancorados nos das
/// laterais"</i>.</para>
///
/// <para>Why a ladder and not one second chance: a piece that failed because the house is not there yet needs the house to
/// grow <b>around it</b>. A single retry at the end of the plan either finds the wall (and the piece is lost for good) or
/// comes too late to save the storey it belongs to — the walk already climbed over a hole. Three rungs, each further from
/// the failure and each with a wider context: the first while the storey is still being built (a few pieces later), the
/// second when the walk finishes that storey (everything the storey holds is up), the third when the whole house is up (the
/// finished building is the most support the piece will ever have). A piece that also fails the third rung is out for good
/// and the order's last line says the number — the counter is per <b>piece</b>, so the three rungs cannot loop.</para>
///
/// <para>Pure on purpose (numbers in, a verdict out): the harness pins the ladder and the insertion point (section (am)) and
/// <c>MHR_Build.RecoverDue</c> adds only the world — the move of the step inside the plan, the counters and the site.</para>
/// </summary>
internal static class BuildRecovery
{
	/// <summary>
	/// How many pieces of the plan this order must <b>place</b> after a piece was given up before the first rung is due — the
	/// player's "depois de umas outras 5 ~10 peças colocadas". Six is the low end of that range on purpose: every piece the
	/// walk places is one more thing holding the next one up, six is already a wall and a floor of the same storey, and the
	/// longer the wait the more likely the walk has climbed over the storey the piece belongs to (the second rung is the one
	/// that needs the storey still being built).
	/// </summary>
	internal const int QueueGap = 6;

	/// <summary>
	/// How many times one piece may come back (the three rungs of <see cref="Tier"/>). It is the piece's own cap, not the
	/// order's: a house with ten unreachable pieces recovers all ten, and a piece the world genuinely cannot take stops after
	/// the third rung instead of holding the companion there for ever.
	/// </summary>
	internal const int MaxTries = 3;

	/// <summary>How much (metres) two pieces may differ in <c>Level</c> and still be the same storey (the blueprint parser
	/// rounds its key to 1 cm, and a storey is 1-2 m).</summary>
	internal const float LevelEpsilon = 0.01f;

	/// <summary>The rung a given-up piece comes back on — the enum is the ladder's order.</summary>
	internal enum Tier
	{
		/// <summary>Not now: the piece waits for its rung (or is out of rungs).</summary>
		None,
		/// <summary>Rung 1: <see cref="QueueGap"/> pieces after the failure, while the storey it belongs to is still being
		/// built.</summary>
		Queue,
		/// <summary>Rung 2: the walk is about to climb above the piece's storey — the storey is as complete as it will get
		/// before the next one starts.</summary>
		Storey,
		/// <summary>Rung 3: the plan's tail, with the whole house up.</summary>
		End
	}

	/// <summary>
	/// Which rung (if any) of the ladder is due <b>now</b>.
	///
	/// <para><paramref name="recovered"/> - how many rungs this piece already took (0 = it was never brought back;
	/// <see cref="MaxTries"/> = out for good).</para>
	///
	/// <para><paramref name="placedSince"/> - how many pieces the order placed since this one was given up <b>or since its
	/// last attempt</b>; <b>-1</b> when the count is unknown (a piece skipped by a site that outlived its worker). Every rung
	/// after the first needs at least <b>one</b> piece to have gone up since the last attempt: a piece that came back and failed
	/// again is not brought back to the same house, and without this the tail would re-attempt it every frame (nothing is
	/// placed there, so nothing changes).</para>
	///
	/// <para><paramref name="walkIndex"/>/<paramref name="planCount"/> - the walk's position: the tail (<c>walkIndex + 1 &gt;=
	/// planCount</c>) is rung 3's place.</para>
	///
	/// <para><paramref name="curLevel"/>/<paramref name="nextLevel"/> - the storey of the piece the walk is <b>on</b> and of the
	/// one it is about to take. <c>nextLevel</c> above <c>curLevel</c> is the walk about to climb - "the end of the storey",
	/// which is rung 2. A next level that is not a number never compares above.</para>
	///
	/// <para><paramref name="reorder"/> - can the caller move a step inside the plan? A <b>site</b> cannot (the plan is the
	/// group's and every worker walks it by index), so there the only rung is the tail, where an append moves nobody.</para>
	/// </summary>
	internal static Tier Decide(bool blueprint, bool reorder, int recovered, int placedSince, int walkIndex, int planCount,
		float curLevel, float nextLevel)
	{
		if (!blueprint || planCount <= 0 || walkIndex < 0 || walkIndex >= planCount
			|| recovered < 0 || recovered >= MaxTries)
		{
			return Tier.None;
		}
		bool tail = walkIndex + 1 >= planCount;
		// something went up since the failure (or since the last attempt): a piece that comes back to an unchanged house meets
		// the same refusal - and at the tail, where nothing is placed anymore, that would be asked again on every frame
		bool changed = placedSince < 0 || placedSince >= 1;
		bool climbed = !float.IsNaN(nextLevel) && !float.IsInfinity(nextLevel)
			&& nextLevel > curLevel + LevelEpsilon;
		if (!reorder)
		{
			// a site: the plan is the group's, so the tail is the only place nobody's index moves - and only when something
			// went up since the last attempt (the site's own retry queue gives a piece its other chances)
			return tail && changed ? Tier.End : Tier.None;
		}
		switch (recovered)
		{
			case 0:
				// rung 1: a few pieces after the failure - or the tail, when the plan already is there (no room for a queue)
				if (tail)
				{
					return Tier.End;
				}
				return placedSince < 0 || placedSince >= QueueGap ? Tier.Queue : Tier.None;
			case 1:
				// rung 2: the walk is about to climb - the storey the piece belongs to is as complete as it will get. The tail
				// is the end of the last storey, so it is the same rung there.
				if (tail)
				{
					return changed ? Tier.End : Tier.None;
				}
				return climbed && changed ? Tier.Storey : Tier.None;
			default:
				// rung 3: the whole construction is up - the most support the piece will ever have
				return tail && changed ? Tier.End : Tier.None;
		}
	}

	/// <summary>
	/// Where the recovered piece goes: a position in the plan <b>after</b> it was taken out of its old place
	/// (<paramref name="walkIndex"/> is the walk's position once the step is out, in a plan of <paramref name="planCount"/>
	/// steps - the move does not change the plan's size). <b>Rung 1</b> lands <see cref="QueueGap"/> steps after the walk (the
	/// walk takes one step per piece, so that is "after the next few pieces went up"); <b>rung 2</b> lands right after the walk
	/// (the storey is over, so there is nothing left of it to wait for); <b>rung 3</b> lands at the tail. A target the plan is
	/// too short for is the tail, never an index past it.
	/// </summary>
	internal static int InsertAt(Tier tier, int walkIndex, int planCount)
	{
		if (planCount <= 0)
		{
			return -1;
		}
		int last = planCount - 1;
		if (tier != Tier.Queue && tier != Tier.Storey)
		{
			return last;   // End: the whole construction is up, the recovered piece waits for the tail
		}
		int gap = tier == Tier.Queue ? QueueGap : 0;
		int target = walkIndex + 1 + gap;
		return target < last ? target : last;
	}
}
