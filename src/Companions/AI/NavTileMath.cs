using System;

namespace Companions;

/// <summary>
/// P2 (multi-build, 03/11/2026): the <b>tile arithmetic</b> of the game's route map, with no Unity in it - so the
/// offline harness can link it and pin the numbers. It is the one axis of <c>Pathfinding.GetTile</c> (decompiled
/// 1.0.20, <c>:797</c>): <c>Mathf.FloorToInt((point + m_tileSize/2) / m_tileSize)</c>. The private dictionary of the
/// game is keyed by <c>Vector3Int(x, z, agent)</c> - the <c>y</c> of the key is the world's Z - and that composition
/// stays in <see cref="MHR_CompanionNavMesh"/>, where the agent is known. Nothing here touches the game.
/// </summary>
internal static class NavTileMath
{
	/// <summary>
	/// The index of the tile that covers <paramref name="world"/> on one axis. The grid is shifted by half a tile, so
	/// the tile 0 spans -size/2 .. +size/2: -16 and +16 (with a 32 m tile) are the two sides of the first seam and
	/// the first tile of each is 0 and 1.
	/// </summary>
	internal static int Axis(float world, float size)
	{
		return (int)Math.Floor((world + size * 0.5f) / size);
	}
}
