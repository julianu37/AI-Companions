using System;

namespace Companions;

/// <summary>
/// P1 (04/11/2026): what a piece of a plan <b>is</b>, for the one decision that depends on it - <b>how far a piece may be
/// moved from the plan's pose</b> when the spot refuses it (<c>too_steep</c>/<c>no_ground</c>, see
/// <c>MHR_Build.NudgePiece</c>).
///
/// <para>A <b>structural</b> piece (floor, wall, roof, beam, pole, stair, door) is part of the house: moved a couple of
/// metres it no longer lines up with the plan and the wall/roof rows stop meeting. <b>Furniture</b> (a chest, a bed, a fire
/// pit, a workbench, a fermenter) has its pose <i>suggested</i> by the blueprint: two metres to the side is the same chest in
/// the same room, and moving it is much better than losing it (measured in the run of 04/11/2026: chests and a bed at y 39,9
/// and 43,9 were refused with <c>too_steep</c> and never adjusted because the old check only looked at
/// <c>m_groundOnly</c>/<c>m_groundPiece</c>).</para>
///
/// <para>Pure on purpose (name → kind, no Unity): the table is asserted by the harness (section (q)), so a new piece whose
/// name falls on the wrong side is caught outside the game. A name the vocabulary does not recognise is <b>furniture</b>
/// (the forgiving side: the piece still gets placed); an empty or null name is <b>structural</b> (a piece nobody named is
/// never moved).</para>
/// </summary>
internal static class BuildPieceKind
{
	/// <summary>How much freedom the plan's pose gives.</summary>
	internal enum Kind
	{
		/// <summary>A floor, wall, roof, beam, pole, stair or door - only a small nudge (≤ 1 m) is acceptable.</summary>
		Structural,
		/// <summary>A chest, a bed, a fire pit, a workbench, a fermenter... - the plan's pose is a suggestion (up to the
		/// search radius).</summary>
		Furniture
	}

	/// <summary>
	/// Classifies a prefab by its name: the structural words are <c>floor</c>, <c>foundation</c>, <c>wall</c>,
	/// <c>roof</c>, <c>beam</c>, <c>pole</c>, <c>stair</c>, <c>ladder</c>, <c>door</c>, <c>gate</c> and <c>pillar</c> - the
	/// same vocabulary the blueprint order uses (<c>BuildBlueprintParser.ClassOf</c>), plus the beams and poles that class 2
	/// mixes with the furniture. Everything else is furniture (a name the vocabulary does not know is the forgiving side: the
	/// piece is placed), except an empty/null name, which stays on the safe side.
	/// </summary>
	internal static Kind Classify(string prefab)
	{
		if (string.IsNullOrEmpty(prefab))
		{
			return Kind.Structural;   // nobody said what it is: never move a piece of an unknown name
		}
		string name = prefab.ToLowerInvariant();
		if (Has(name, "floor") || Has(name, "foundation") || Has(name, "wall") || Has(name, "roof")
			|| Has(name, "beam") || Has(name, "pole") || Has(name, "stair") || Has(name, "ladder")
			|| Has(name, "door") || Has(name, "gate") || Has(name, "pillar"))
		{
			return Kind.Structural;
		}
		return Kind.Furniture;
	}

	/// <summary>Convenience: may this piece only be nudged by the small radius? (see <see cref="Kind.Structural"/>).</summary>
	internal static bool IsStructural(string prefab)
	{
		return Classify(prefab) == Kind.Structural;
	}

	/// <summary>
	/// Is this prefab a buried TIP - a pole, a beam or a pillar? Only a tip may be settled up to the terrain on its own:
	/// its own length absorbs the move, so the piece above still lands on its top (measured: the reference house has 3
	/// tips buried 2 m and a 1 m lift, and the floors sit on them).
	/// <para>A floor, a wall, a roof or a door moved alone opens a gap above it and the game destroys the piece that is
	/// left with nothing under it. Measured on the Forest Stone Cottage (04/11/2026): its stone foundation is 1,48 m
	/// under its wooden floor, the config lifted the plan 1,00 m, the settle moved the 16 foundation pieces to the
	/// ground and the floors above stayed at +1,00 m - every one of them came back "verification failed: not_found".</para>
	/// </summary>
	internal static bool IsTip(string prefab)
	{
		if (string.IsNullOrEmpty(prefab))
		{
			return false;   // an unknown name is never moved on its own (the safe side, same as Classify)
		}
		string name = prefab.ToLowerInvariant();
		return Has(name, "pole") || Has(name, "beam") || Has(name, "pillar");
	}

	private static bool Has(string name, string part)
	{
		return name.IndexOf(part, StringComparison.Ordinal) >= 0;
	}
}
