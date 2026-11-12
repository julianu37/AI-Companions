namespace Companions;

/// <summary>
/// E7: <b>why</b> a <c>.blueprint</c> line does not become part of the plan - the pure half of the two rules the build
/// order applies to every line before the first piece is paid for: the prefab has to be a buildable <c>Piece</c> in this
/// game (a blueprint is somebody else's design and may carry an item, a rug or a mod's prefab) and the local player has
/// to know its recipe. The caller answers the two game questions (the prefab through <c>BuildProbe.PieceOf</c>, the
/// recipe through <c>Player.IsRecipeKnown</c>) and this class decides - and <b>names</b> - the outcome, so the order's
/// log (<c>MHR_Build.StartBlueprint</c>) and the DevBridge's <c>blueprints.inspect</c> can never disagree.
/// <para>Unity-free and dependency-free on purpose: the two strings below are also the report's <c>skippedReasons</c>.</para>
/// </summary>
internal static class BuildBlueprintSkip
{
	/// <summary>The prefab is not a buildable piece here (an item, a rug, a mod's prefab, a name this game does not have).</summary>
	internal const string NotAPiece = "not a piece here";

	/// <summary>The prefab is a piece, but the local player has not unlocked its recipe in this world.</summary>
	internal const string RecipeLocked = "recipe not unlocked";

	/// <summary>
	/// The reason this line is <b>not</b> placed, or null when it is part of the plan. <paramref name="recipeKnown"/>
	/// only means something for a prefab that resolved to a piece: a line without one is already skipped for that, and
	/// the ordinary value there is <c>false</c>.
	/// </summary>
	internal static string Reason(bool hasPiece, bool recipeKnown)
	{
		if (!hasPiece)
		{
			return NotAPiece;
		}
		if (!recipeKnown)
		{
			return RecipeLocked;
		}
		return null;
	}
}
