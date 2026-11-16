namespace Companions;

/// <summary>
/// 15/11/2026 (the solo run of <c>CommHomeBasicBP</c>, measured: <b>653 placed of 985</b> and <b>135</b> pieces given up, with
/// the body frozen at <c>y = 39,9</c> while every remaining target was at <c>y = 42,8</c>): <b>a piece of a storey above is
/// never taken while a piece below it is still unresolved</b> - the player's rule
/// <i>"todos os blueprints devem ser construídos debaixo pra cima"</i> and <i>"ele tinha que tentar colocar de novo antes de
/// continuar subindo"</i>.
///
/// <para>The defect it closes is a <b>measurement that never decided anything</b>. <c>MHR_Build.ServeBarrier</c> waited
/// <see cref="BuildFailure.BodyRetryMaxWait"/> (10 s) for the storey below, then logged
/// <i>"fica na fila do canteiro e o plano sobe"</i> and let the walk climb - while <c>PendingAbove</c> printed
/// <b>63 piso(s) pendente(s) acima</b> into that same line and was read by nothing. Worse, <c>PendingBelowIndex</c> only
/// looks at pieces <b>already in the retry queue</b>, so a piece of the ground floor the walk had never even tried did not
/// count as "waiting below" at all: the house went up with holes in the floor it was standing on.</para>
///
/// <para>Pure on purpose (numbers in, a verdict out): the harness pins the table (section (aq)) and
/// <c>MHR_Build.PendingBelowPlan</c> adds only the plan - which indices are settled, and at which <c>Level</c>.</para>
/// </summary>
internal static class BuildStoreyGate
{
	/// <summary>
	/// How much (metres) two pieces may differ in <c>Level</c> and still count as the <b>same</b> storey. It matches
	/// <see cref="BuildRecovery.LevelEpsilon"/>: the blueprint parser rounds its key to 1 cm, and a storey of the measured
	/// house is 1-2 m, so 1 cm separates storeys without ever splitting one.
	/// </summary>
	internal const float SameStorey = 0.01f;

	/// <summary>
	/// May the walk take a piece at <paramref name="nextLevel"/>?
	///
	/// <para><paramref name="pendingBelow"/> - how many pieces of the plan are <b>below</b> that level and still
	/// unresolved (neither placed nor given up). The caller counts them over the <b>whole plan</b>, not over the retry
	/// queue: that is the hole this rule closes. Anything at or below zero lets the piece be taken.</para>
	///
	/// <para><paramref name="scaffoldingAvailable"/> - may the caller still put a temporary stair down? It never unlocks the
	/// climb: a stair is how the companion reaches the piece that is <b>pending below</b>, so the answer is the same
	/// either way. It is here because the caller must not <b>wait standing still</b> for a piece it can no longer get to -
	/// with scaffolding it serves the piece below, without it the piece goes back to the queue and the walk keeps
	/// working. The verdict on the climb is identical, which is exactly what makes this a gate and not a suggestion.</para>
	///
	/// <para>A level that is not a number (a template, a single piece: <c>Level</c> 0) never blocks anything - those plans
	/// have no storeys to climb.</para>
	/// </summary>
	internal static bool MayTake(float nextLevel, int pendingBelow, bool scaffoldingAvailable)
	{
		if (float.IsNaN(nextLevel) || float.IsInfinity(nextLevel))
		{
			return true;
		}
		return pendingBelow <= 0;
	}

	/// <summary>
	/// Is the plan step at <paramref name="level"/> <b>below</b> the storey the walk is about to take
	/// (<paramref name="nextLevel"/>)? A piece of the <b>same</b> storey is never "below": the wall of the 1st floor has
	/// the same <c>Level</c> as the beam its tiles rest on (Run 10), and holding a storey against itself would stop the
	/// plan dead.
	/// </summary>
	internal static bool Below(float level, float nextLevel)
	{
		if (float.IsNaN(level) || float.IsNaN(nextLevel) || float.IsInfinity(level) || float.IsInfinity(nextLevel))
		{
			return false;
		}
		return level < nextLevel - SameStorey;
	}

	/// <summary>
	/// The blueprint parser's <b>door</b> class (<c>BuildBlueprintParser.ClassOf</c> 4: a <c>door</c>/<c>gate</c>). A door is
	/// never what holds a piece up and is never what the plan is waiting for - it is the last thing of the house, on purpose
	/// (the player's rule: <i>"as portas precisam ser os últimos itens colocados, pois se não ele perde muito tempo abrindo e
	/// fechando elas toda hora pra passar"</i>).
	/// </summary>
	internal const int DoorClass = 4;

	/// <summary>
	/// Is the plan piece at <paramref name="level"/> / <paramref name="pieceClass"/> something the gate must <b>settle
	/// before climbing</b>?
	///
	/// <para><b>A door is excluded, and that exclusion is the whole point of this method.</b> The gate was written asking
	/// only <see cref="Below"/>, so a door of the storey below counted as a pending piece and was then <b>served</b> by
	/// <see cref="LowestPendingBelow"/> - it was pulled forward out of the tail of the plan and built in the middle of the
	/// house. Measured on <c>CommHomeBasicBP</c> (985 pieces, 16/11/2026): the 7 <c>wood_gate</c> at plan indices
	/// <b>979-985</b> - the last 7 of the file, in the tail where <c>BuildBlueprintParser</c> puts class 4 - were placed as
	/// pieces <b>412 to 418</b> of 510, i.e. as soon as the storey below them filled up. The older retry path already knew
	/// this (<c>MHR_Build.BelowIndex</c>: <c>ClassOf == 4 ? -1 : index</c>); the gate's own two methods forgot it, and the
	/// door came back to the middle of the build.</para>
	///
	/// <para>It excludes doors from <b>both</b> answers on purpose: from <c>pendingBelow</c> (a door left for the end never
	/// holds the climb - otherwise a plan whose doors are the last 7 pieces would never be allowed to climb at all, and the
	/// order would deadlock on the pieces it is supposed to end with) and from the piece the gate serves (a door is never
	/// what the floor is waiting for). The door's own turn is the tail of the plan, exactly where the parser put it.</para>
	/// </summary>
	internal static bool CountsAsPending(float level, float nextLevel, int pieceClass)
	{
		if (pieceClass == DoorClass)
		{
			return false;
		}
		return Below(level, nextLevel);
	}

	/// <summary>
	/// May the gate <b>serve</b> a pending piece now, or must it respect the wait the queue just gave it?
	///
	/// <para>Measured on <c>CommHomeBasicBP</c> (16/11/2026), given verbatim: the companion fell walking to the
	/// <c>piece_chest_wood</c> of piece <b>851</b>, the fall path put the piece back in the queue with its wait
	/// (<c>retry later: piece_chest_wood (falls) at (-152,5, 44,9, -427,1) - 1/3</c>), and <b>the next line</b> is the gate
	/// serving the same piece again (<c>voltando para piece_chest_wood (851/985)</c>) - then <c>fell 6,8 m and took 18 fall
	/// damage</c> and the death. The wait existed and the gate walked over it, so the piece was retried on the same
	/// breath, at the same edge, with the companion still there.</para>
	///
	/// <para>The gate asked <c>LowestPendingBelow</c>, which reads the <b>plan</b> and never the queue's clock - while
	/// <c>NextRetryBelow</c>, the older path, has always checked <c>Time.time &lt; state.RetryAt</c>. This is the same rule
	/// for the same question: a piece that is <b>queued and not due yet</b> is not servable, so the gate serves the next
	/// pending piece or waits. It never weakens the climb - the still-pending piece keeps holding the storey, which is the
	/// player's rule; it only changes <b>which</b> piece gets the turn.</para>
	/// </summary>
	/// <param name="queued">Is the piece in the retry queue (it failed and is waiting)?</param>
	/// <param name="due">Has its wait elapsed (<c>Time.time &gt;= RetryAt</c>)?</param>
	internal static bool Servable(bool queued, bool due)
	{
		return !queued || due;
	}
}
