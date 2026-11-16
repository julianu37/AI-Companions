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
}
