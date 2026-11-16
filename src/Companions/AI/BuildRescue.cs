namespace Companions;

/// <summary>
/// 15/11/2026 (the solo run of <c>CommHomeBasicBP</c>, measured: the body did not move for the whole tail of the order -
/// <c>(-149,4, 39,9, -430,2)</c> against targets at <c>y = 42,8</c>, <c>route partial</c> on <b>650</b> lines, and the log
/// repeating <c>2,1m below where it needs to be - jumping up onto (…) (1,1m up, 2,6m on)</c>): the player's verdict
/// <i>"esta preso ele, isso não pode ser tolerado, afinal é uma função de companions que constroem blueprints, não de
/// companions enjaulados"</i>. A companion that <b>builds</b> may not stand still for ever.
///
/// <para>The rescue is the last resort, and it is deliberately narrow - the player picked it as
/// <i>"teleporte discreto, contado"</i>, never the player, never silently:</para>
/// <list type="number">
/// <item><b>ROTA PARCIAL por <see cref="RoutePartialAfter"/> s</b> - the mover already knows the only way to the piece is
/// not walkable (<c>MHR_Mover.RoutePartial</c>). A complete route is a companion that is simply walking.</item>
/// <item><b>o degrau temporário já falhou</b> - <c>TryScaffold</c> had its chance for this piece. A stair is the honest
/// way up and is always preferred: it leaves the world in a state the player can see and undo.</item>
/// <item><b>nao saiu do lugar</b> - not one metre closer and not one metre higher for the same <see cref="StuckFor"/>
/// s. This is what separates "caged" from "taking the long way round a big house".</item>
/// </list>
///
/// <para>Pure on purpose (numbers in, a verdict out): the harness pins the table (section (ar)) and
/// <c>MHR_Build.TryRescue</c> adds only the world - a walk spot from <c>BuildWalkSpot</c> and the
/// <c>ai.MHR_PlaceAt</c> the mod already uses in <c>MHR_Mover.Rescue</c>.</para>
/// </summary>
internal static class BuildRescue
{
	/// <summary>
	/// How long the mover must report a <b>partial</b> route before the walk is considered caged. 30 s is three times the
	/// <c>no progress</c> clock of 12 s of the same job: a piece refused twice by the walk is a piece the walk cannot
	/// reach, not a piece it is fumbling.
	/// </summary>
	internal const float RoutePartialAfter = 30f;

	/// <summary>
	/// How long the body must stay inside <see cref="Moved"/> of where it was before the second condition is met - no
	/// horizontal gain and no vertical gain. A companion walking around a 20 m house moves; a companion jumping at a
	/// ledge 1,3 m above its head does not (measured: four <c>2,1m below</c> lines in a row at the same pose).
	/// </summary>
	internal const float StuckFor = 30f;

	/// <summary>How far (metres, 3D) the body must move in that window to count as "it is walking". Generous on purpose:
	/// a companion on a ladder chain covers less than this between two frames but far more than it across 30 s.</summary>
	internal const float Moved = 1.5f;

	/// <summary>
	/// How many teleports one order may spend. The player's word was "máx. 3 por obra", and the number is also the
	/// <b>falsifier</b> of the whole cycle: if a run needs the last one, the temporary stair (item 3) is not doing its job
	/// and the run is a failure even with <c>skipped = 0</c> - the teleport must never be the crutch.
	/// </summary>
	internal const int MaxPerOrder = 3;

	/// <summary>
	/// How far (metres) from the order's anchor a rescued body may be put. The walk spot comes from the plan's own
	/// structural surface, so it is already inside the site; this only keeps a rescued body from being handed to a spot
	/// the plan happens to hold across a river.
	/// </summary>
	internal const float MaxFromAnchor = 40f;

	/// <summary>
	/// Should this piece be rescued right now?
	///
	/// <para><paramref name="partial"/> - <c>MHR_Mover.RoutePartial(ai)</c>: the mover knows it cannot walk there.</para>
	/// <para><paramref name="partialFor"/> - how many seconds that has been true without a break. A route that became
	/// complete resets it; the caller must pass 0 when <paramref name="partial"/> is false.</para>
	/// <para><paramref name="scaffoldTried"/> - did <c>TryScaffold</c> already try (and fail) for this piece?</para>
	/// <para><paramref name="stillFor"/> - how many seconds the body has stayed inside <see cref="Moved"/> of where this
	/// piece found it.</para>
	/// <para><paramref name="walkSpotOk"/> - is there a structural surface (from <c>BuildWalkSpot</c>) to be put on at
	/// all? Without one there is nowhere honest to go, and the walk goes back to the queue instead.</para>
	/// <para><paramref name="spent"/> - teleports this order already spent (<see cref="MaxPerOrder"/> is the cap).</para>
	/// </summary>
	internal static bool Should(bool partial, float partialFor, bool scaffoldTried, float stillFor, bool walkSpotOk,
		int spent)
	{
		if (!partial || !scaffoldTried || !walkSpotOk)
		{
			return false;
		}
		if (spent < 0 || spent >= MaxPerOrder)
		{
			return false;
		}
		return partialFor >= RoutePartialAfter && stillFor >= StuckFor;
	}

	/// <summary>
	/// Is <paramref name="spot"/> close enough to the order's <paramref name="anchor"/> to be a rescue target? The distance
	/// is measured <b>flat</b>: a storey of the same house is not a detour.
	/// </summary>
	internal static bool InBounds(float flatFromAnchor)
	{
		return !(flatFromAnchor > MaxFromAnchor) && !float.IsNaN(flatFromAnchor);
	}
}
