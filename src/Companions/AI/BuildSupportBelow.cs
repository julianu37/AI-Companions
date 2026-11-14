using System;

namespace Companions;

/// <summary>
/// P1 (04/11/2026, measured on the 985-piece house with 3 companions): what can be the <b>direct support</b> of a piece of
/// a plan - the piece below it that the game's support rule (<c>WearNTear</c>) reads. It is the question a piece refused
/// with <c>no_support</c> asks: <i>what is it waiting for?</i>
///
/// <para>The measured run left ~10 pieces of the <b>upper storey</b> behind (<c>wood_floor</c>/<c>wood_roof_45</c> at y 43,8
/// and 45,8). In a group of workers the plan is walked by <b>index</b> and nobody reorders it (the indices are the site's,
/// shared by the claims), so the piece that would hold them up may still be <b>ahead</b> in the plan - or it was skipped by
/// a worker whose turn it was and nobody went back to it. Either way the piece waits in the queue as a <b>dependency</b>
/// (no attempt spent, growing waits) until the stall watchdog gives up on it after 60 s without progress. The pull of
/// <c>MHR_Build.PullDirectSupport</c> drives that support <b>next</b>, by index, without touching the plan.</para>
///
/// <para><b>The window</b> (2,5 m in XZ, 0,4 to 2,5 m below) is the one a piece is held up by: the storeys of the measured
/// house are 1 m in the blueprint's floors and 2 m in walls/roofs, so a support at 2 m is a wall/beam top and one at 1 m is
/// the floor tile under the piece. A piece <b>less than 0,4 m below</b> is a neighbour of the same storey (a beam lying on
/// the same floor, another floor tile) - it never holds the refused piece up - and the <b>drop is always positive</b>, so
/// the pull only ever walks <b>down</b> the plan and cannot loop.</para>
///
/// <para>Pure on purpose (arithmetic only, no Unity): the window and the ranking are asserted by the harness (section (r)),
/// so a bad number here is caught outside the game.</para>
/// </summary>
internal static class BuildSupportBelow
{
	/// <summary>How far in XZ (metres) the support may be from the piece it holds up.</summary>
	internal const float MaxFlat = 2.5f;

	/// <summary>How far below (metres) the support may be - a wall/roof top two storeys down is not what holds a piece up.</summary>
	internal const float MaxDrop = 2.5f;

	/// <summary>How far below (metres) it has to be at least - closer than this it is a neighbour of the same storey, not a
	/// support (and it is what makes the pull strictly descend, so it cannot loop).</summary>
	internal const float MinDrop = 0.4f;

	/// <summary>
	/// Is the candidate at (<paramref name="dx"/>, <paramref name="drop"/>, <paramref name="dz"/>) - <b>relative to the
	/// piece it would hold up</b> - its direct support? <paramref name="drop"/> is positive when the candidate is
	/// <b>below</b> the piece (this floor's Y minus the candidate's Y).
	/// </summary>
	internal static bool IsDirectSupport(float dx, float dz, float drop)
	{
		if (drop < MinDrop || drop > MaxDrop)
		{
			return false;   // above it, on its own storey, or too far down to be what holds it
		}
		return dx * dx + dz * dz <= MaxFlat * MaxFlat;
	}

	/// <summary>
	/// How good a candidate is (smaller is better): the <b>drop weighs 10x the flat distance</b>, because what holds a piece
	/// up is what is under it - the closest below wins over a neighbour that is nearer but lower (a floor tile at 1 m in XZ
	/// loses to the beam top 0,5 m below it, which is the piece the game actually reads).
	/// </summary>
	internal static float Rank(float dx, float dz, float drop)
	{
		return drop * 10f + (float)Math.Sqrt(dx * dx + dz * dz);
	}

	/// <summary>
	/// P1 (04/11/2026, measured: the emergency post of the deck run was planted with its CENTRE 1 m under the piece, so a
	/// piece 1 m above the ground got a 2 m post buried 1 m deep and the game refused the post itself with
	/// <c>no_support</c>): which of the available posts reaches a gap? Returns the <b>index</b> of the shortest post that
	/// does, or -1 (nothing that reaches it: the caller does not plant one - stacking would be needed).
	///
	/// <para><paramref name="lengths"/> are the real lengths of the posts this mod can build, measured from each prefab's own
	/// snap points (0 or less = the prefab is not in this game and is skipped). A post <b>reaches</b> when it is at most
	/// <see cref="ShortTolerance"/> shorter than the gap and at most <see cref="MaxOverlap"/> longer (see both for the
	/// measured numbers). The shortest one wins: the post must look like the one a player would pick.</para>
	///
	/// <para><b>The windows</b> (11/11/2026, measured on the 987-piece house), per post length: <c>wood_beam_1</c> (1 m)
	/// covers <c>[0,55; 1,20]</c>, <c>wood_beam</c>/<c>wood_pole2</c> (2 m) cover <c>[1,55; 2,20]</c> and
	/// <c>wood_beam_45</c> (2,83 m) covers <c>[2,38; 3,03]</c>. The gaps of the house that had no post are <b>0,60 m</b>
	/// (the step between two floors, below the 1 m window before this change) and <b>1,60 m</b> (between the 1 m and the
	/// 2 m ones). Two gaps are left with <b>no post at all</b> in vanilla: <c>1,20-1,55</c> and <c>2,20-2,38</c> - there is
	/// no piece between 1 m and 2 m, and the 2,83 m one is the next step.</para>
	/// </summary>
	internal static int PickPost(float[] lengths, float gap)
	{
		if (lengths == null)
		{
			return -1;
		}
		int best = -1;
		float bestLength = float.MaxValue;
		for (int i = 0; i < lengths.Length; i++)
		{
			float length = lengths[i];
			if (length <= 0.01f || length < gap - ShortTolerance || length > gap + MaxOverlap)
			{
				continue;
			}
			if (length < bestLength)
			{
				bestLength = length;
				best = i;
			}
		}
		return best;
	}

	/// <summary>How much shorter than the gap a post may be. 0,2 m is the slack of the measured ladder (1 m under a 1,2 m gap,
	/// 2 m under a 2,2 m); the game's support box is grown by 0,15 m, so a post at the very edge sits 5 cm short and the piece
	/// is simply retried - better than a post that does not fit.</summary>
	internal const float ShortTolerance = 0.2f;

	/// <summary>How much longer than the gap a post may be. A little overlap is how the pieces of the house already meet,
	/// and the game reads support by <b>collider overlap</b> (<c>WearNTear.SetupColliders</c> grows each box by 0,15 m, and
	/// the placement itself does not test support), so the top of the post may sit <b>through</b> the piece it holds.
	///
	/// <para>P1 (11/11/2026, measured on the 987-piece house: the gap of the <c>wood_floor</c> step is <b>0,60 m</b>, and with
	/// 0,35 the shortest post, <c>wood_beam_1</c> (1 m), did not fit - its top stands 0,40 m above the piece): 0,45 turns the
	/// 1 m window into <c>[0,55; 1,20]</c> and covers the 0,60 m gap, with the beam crossing 0,40 m of the piece it holds -
	/// which the game accepts, because the bond is the collider overlap. A whole storey (2 m) would still go through the
	/// piece: the cap stays well under that.</para>
	/// </summary>
	internal const float MaxOverlap = 0.45f;

	/// <summary>
	/// P1 (04/11/2026, measured: the post ray of a floor at 40,8 stopped on a roof eave <b>0,16 m</b> under it - "vão de
	/// 0,16 m" 318 times - and no post fits in 16 cm, while the surface a post wants, the deck floor, was 1,02 m down): the
	/// first surface of <paramref name="depths"/> that is <b>deep enough to be a base</b>, i.e. at least
	/// <paramref name="minGap"/> under the piece <b>and</b> that can hold a post (<paramref name="holds"/>).
	/// <paramref name="count"/> entries are read (the caller fills fixed buffers in the order the ray met them). Returns the
	/// index of that surface, or -1 when none of them is usable (the caller then uses the terrain height).
	///
	/// <para>P1 (11/11/2026, the run that closed at 950 of 987 with <b>95 refusals</b> of the post itself and 20 pieces that
	/// never went up): depth alone was not enough - a deep hit can still be a surface that <b>cannot carry a post</b>: the top
	/// of a <b>chest</b>, a bed or a fireplace (furniture is the only class with <c>m_supports = false</c> in 1.0.20), where
	/// the post was accepted, had nothing under it and the game refused the post with <c>no_support</c>. The surface has to
	/// <b>hold</b>, not just exist: the caller asks <see cref="HoldsPlacedPiece"/> for every hit and the walk goes on through
	/// the ones that do not.</para>
	///
	/// <para>Pure on purpose (floats and booleans only): the walk itself is a Unity raycast, this is the decision the harness
	/// asserts (section (r)).</para>
	/// </summary>
	internal static int UsefulSurface(float[] depths, bool[] holds, int count, float minGap)
	{
		if (depths == null || holds == null)
		{
			return -1;   // no measurement (or nothing known about what holds): the caller uses the terrain
		}
		int read = count < depths.Length ? count : depths.Length;
		if (holds.Length < read)
		{
			read = holds.Length;   // never read past the flags the caller filled
		}
		for (int i = 0; i < read; i++)
		{
			if (depths[i] >= minGap && holds[i])
			{
				return i;
			}
		}
		return -1;
	}

	/// <summary>
	/// P1 (04/11/2026, measured on the 985-piece house): can a piece of the <paramref name="candidateClass"/> class
	/// (<c>BuildBlueprintParser.ClassOf</c>) hold up a piece of the <paramref name="pieceClass"/> class?
	///
	/// <para>A <b>door</b> (4) never holds anything up - and pulling one would break "doors last". A <b>roof</b> (3) holds
	/// only a <b>roof</b>: in a gable the row of a slope is supported by the row below it, which is also a roof (roof 45 over
	/// the wall, gable over the slope), while a floor is never held by a roof above it. Everything else that is structural
	/// (floors, walls, beams, poles, stairs) holds up anything that is not a door. The east gable of that run stayed open
	/// because the pull refused every roof as a support and never fired.</para>
	/// </summary>
	internal static bool CanSupport(int pieceClass, int candidateClass)
	{
		if (candidateClass == 4)
		{
			return false;   // a door is never what holds a piece up (and it is built last on purpose)
		}
		if (candidateClass == 3)
		{
			return pieceClass == 3;   // roof on roof: the row below the slope, never the roof over a floor
		}
		return true;
	}

	/// <summary>
	/// P1 (04/11/2026, measured: 13 roof pieces stayed out for FIVE rounds and the emergency post was refused 318 times with
	/// "vão de 0,16 m"): can a collider found inside the footprint of a piece of <paramref name="pieceClass"/> hold it?
	///
	/// <para>The terrain holds anything. Every other piece needs a <see cref="WearNTear"/> that really
	/// <b>carries</b> - <paramref name="wearSupports"/>, i.e. <c>m_supports</c> - whatever its class is (floor, wall,
	/// roof, beam, pole, stair, door). A rock or a tree (no <see cref="WearNTear"/>) is not a support.</para>
	///
	/// <para><b>Why no class is exempt (measured, 14/11/2026: the player asked "por que cama como ancora?" over the two
	/// roof holes of his screenshot):</b> this method used to have a bypass - <c>pieceClass == 3 || wearSupports</c>, every
	/// roof accepted by <b>any</b> <see cref="WearNTear"/>, furniture included. The bypass was written (04/11/2026) on the
	/// belief that "a roof piece has <c>m_supports = false</c>", and that belief was <b>already corrected</b> on 11/11/2026:
	/// <c>wood_roof</c>, <c>wood_roof_45</c>, <c>wood_roof_top</c>, <c>wood_roof_top_45</c>, <c>wood_roof_icorner_45</c>,
	/// <c>wood_floor</c>, <c>wood_wall_half</c>, <c>wood_stair</c>, <c>wood_stepladder</c>, <c>wood_pole2</c> and
	/// <c>wood_beam_1</c> are all <c>m_supports = true</c>; <b>only furniture is <c>false</c></b>
	/// (<c>docs/re/support-rule-wearntear.md</c> §5). With every structural prefab <c>true</c>, the bypass is a
	/// <b>no-op</b> for roof over roof and for roof over a wall - and its only live effect was the <b>wrong</b> one: a roof
	/// standing on a <b>chest</b> or a <b>bed</b> passed here, though the game never reads furniture as support
	/// (<c>UpdateSupport</c> skips the collider on <c>!m_supports</c>, <c>WearNTear.cs:920-923</c>) and the vanilla ghost
	/// refuses to build on it at all (<c>Player.cs:3890-3893</c>). A bed is not an anchor: it made the probe say "held"
	/// where the game would have made the piece fall.</para>
	///
	/// <para>Sibling of <see cref="CanSupport"/> (the pull's rule): there a roof only holds a <b>roof</b>; here a roof is
	/// only <b>held</b>, and by the same rule as everything else - the piece below has to carry. Both agree that a door never
	/// holds and that a roof never holds a floor.</para>
	/// </summary>
	internal static bool HoldsPlacedPiece(int pieceClass, bool isTerrain, bool hasWear, bool wearSupports)
	{
		// pieceClass is kept in the signature on purpose: it is what the caller measured (BuildBlueprintParser.ClassOf) and
		// what the harness prints per case, so a future rule that really needs the class does not have to change the call
		// sites of BuildProbe and MHR_Build. Since 14/11/2026 no class is exempt - the flag alone decides.
		if (isTerrain)
		{
			return true;   // the ground holds every piece, whatever it is
		}
		if (!hasWear)
		{
			return false;   // a rock, a tree, an item on the ground: not a support
		}
		return wearSupports;   // furniture never holds - not even a roof (it is the only class with m_supports = false)
	}

	/// <summary>
	/// 11/11/2026 (the Run 9: the <c>wood_floor</c> tile at 39,8 was refused by a body, went to the site's queue, and the
	/// guard "never a post where the plan has a support" then blocked the emergency post for the three floors above it -
	/// the support was <b>in the plan</b> but <b>not in the world</b>): may the emergency post be planted under a piece whose
	/// direct support <b>exists in the plan</b>?
	///
	/// <para>Only when the plan's own support is <b>not coming</b>: it was <b>given up</b> (it is in the skipped set - nobody
	/// will build it), or <b>nobody holds it</b> and the refused piece has already been deferred <see cref="SupportGrace"/>
	/// times. A support another companion <b>claims</b> is on its way - the piece only waits. The deferrals are the clock the
	/// plan had to deliver it: the waits are 8 s, 12 s, 20 s, 25 s (<c>MHR_Build.DependencyDelay</c>), so the third look is
	/// ~20 s after the first refusal - long enough for a worker to walk to the support and place it.</para>
	///
	/// <para>Pure on purpose (three numbers): the harness asserts the table (section (u)).</para>
	///
	/// <para>13/11/2026 (the solo run of <c>CommHomeBasicBP</c>, 985 pieces, 23 emergency posts measured): does something the game
	/// reads as a support already <b>stand</b> close enough that a refusal by <c>no_support</c> cannot be about a support
	/// that is missing? This is the guard the post was missing.
	///
	/// <para><b>What was measured</b> (<c>docs/backlog-construcao.md</c> item 19): the 23 posts of that run were planted
	/// with <b>no wait at all</b> - <c>next:</c>, the post, <c>placed</c>, on consecutive log lines. The reach the game has
	/// is <b>0,15 m of collider overlap</b> (<c>WearNTear.SetupColliders</c>), which is what <c>BuildProbe.FootprintSupported</c>
	/// already grows the piece's box by - so the probe's own verdict cannot reach 2,5 m (2 × 2 floor box + 0,15 = 2,3 m of
	/// reach in the best case, and the support of this house sits at 0,9-1,0 m of its centre, <b>inside</b> that box), and a
	/// <c>no_support</c> can come from a collider that is <b>not there yet</b> (the piece is placed in the same frame the
	/// neighbour is, and the overlap is asked before it exists).
	/// With the support standing where the plan puts it, the post is <b>guessed</b>, not measured: the piece's own failure
	/// has another cause and planting a post neither fixes it nor is it cheap (a post is not in the plan - 23 of them are 23
	/// pieces of wood a player did not ask for, and this house needed <b>none</b> to stand).</para>
	///
	/// <para><b>The rule</b> (pure, asserted in harness section (u)): a support the plan <b>has</b> keeps the old
	/// <see cref="FallbackPostAllowed"/> window (given up / nobody holds it / <see cref="SupportGrace"/> deferrals). With
	/// <b>no</b> support in the plan's window (<paramref name="planSupportFound"/> false - every candidate already standing,
	/// or none ever planned) the post waits the <b>same</b> three looks: a support standing at
	/// <paramref name="supportClose"/> truly cannot carry it (the game's own <c>WearNTear</c> says so at placement, and the
	/// post is then the way up), and a support standing further away is <b>not</b> evidence that nothing can - the box test
	/// reaches 4 m, so the piece gets the same ~20 s before the post is planted on a guess.</para>
	/// </summary>
	internal static bool FallbackPostAllowed(bool supportGivenUp, bool supportClaimed, int supportDeferrals, bool planSupportFound, bool? supportClose)
	{
		if (supportGivenUp)
		{
			return true;   // the plan's support is out of the site for good: the post is the only way up
		}
		if (planSupportFound)
		{
			if (supportClaimed)
			{
				return false;   // somebody is building it right now - the piece waits for it
			}
			return supportDeferrals >= SupportGrace;
		}
		// no support in the plan's window: a standing one close enough to be read by the game's collider overlap is proof
		// that the refusal is not "nothing under it" - the post does not fix a piece the game really refuses, it only pays
		// for wood nobody asked for. A standing one further away, or a scan that did not run (null - a caller without the
		// world in hand keeps the old behaviour), is no proof either way: same window as the plan's own.
		return supportClose != true && supportDeferrals >= SupportGrace;
	}

	/// <summary>How far from a piece's centre a support the game can read may be: the reach of the collider overlap for
	/// the widest piece of the plans (a 2 × 2 floor is 1,15 m from its centre to a corner, plus the 0,15 m the game grows
	/// each box by). A support inside it stands where <c>WearNTear</c> can hold the piece, so a refusal there is <b>not</b>
	/// an absent support (<see cref="FallbackPostAllowed"/>).</summary>
	internal const float SupportClose = 1.3f;

	/// <summary>The three-number form: no scan of the world was made (<see cref="FallbackPostAllowed(bool,bool,int,bool,bool?)"/>
	/// with <c>supportClose = null</c>), which is exactly the behaviour every caller had before 13/11/2026.</summary>
	internal static bool FallbackPostAllowed(bool supportGivenUp, bool supportClaimed, int supportDeferrals)
	{
		return FallbackPostAllowed(supportGivenUp, supportClaimed, supportDeferrals, true, null);
	}

	/// <summary>How many times a piece has to be deferred by <b>dependency</b> before the plan's own support counts as "not
	/// coming" and the emergency post takes over (<see cref="FallbackPostAllowed"/>). Two: the third look is ~20 s after the
	/// first refusal (8 s + 12 s of <c>MHR_Build.DependencyDelay</c>), which is the time the plan had to put the support
	/// up.</summary>
	internal const int SupportGrace = 2;
}
