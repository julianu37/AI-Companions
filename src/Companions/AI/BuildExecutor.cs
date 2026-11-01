using System;
using System.Collections.Generic;
using Splatform;
using UnityEngine;

namespace Companions;

/// <summary>Which code path placed the piece (the log says it, so a test in the game is never ambiguous).</summary>
internal enum BuildRoute
{
	None,
	/// <summary><c>Player.PlacePiece</c> - the vanilla path, with the owner's <c>Player</c> loaded here.</summary>
	PlayerPlacePiece,
	/// <summary>Our own <c>Instantiate</c> + <c>OnPlaced</c> + <c>SetCreator</c> sequence (no <c>Player</c> needed).</summary>
	Direct
}

/// <summary>The outcome of one placement: the object (for the caller to verify or destroy), the route used and,
/// when it failed, why.</summary>
internal sealed class BuildResult
{
	internal bool Ok;
	internal BuildRoute Route;
	internal string Error;
	internal GameObject GameObject;
	internal Piece Piece;
	internal long Creator;

	internal static BuildResult Fail(BuildRoute route, string error)
	{
		return new BuildResult { Ok = false, Route = route, Error = error };
	}
}

/// <summary>
/// Fase E1: the <b>executor</b> - it makes a piece exist in the world. Two routes, one entry point:
///
/// <list type="number">
/// <item><b><see cref="BuildRoute.PlayerPlacePiece"/></b>: when the owner's <c>Player</c> is the local one (single
/// player and host - the normal case), <c>Player.PlacePiece(piece, pos, rot, doAttack: false)</c> does everything
/// vanilla does: <c>Instantiate</c>, <c>AddKnownStation</c> (the player learns the station), <c>PrivateArea.Setup</c>,
/// <c>WearNTear.OnPlaced</c>, <c>IPlaced.OnPlaced</c>, <c>ItemDrop.MakePiece</c>, the place effect, the build stats
/// and the creator = the player. It is the route the BuildOrders mod uses for its own helper
/// (<c>player.PlacePiece(piece, o.Pos, o.Rot, doAttack: false)</c>), so it is the best tested one.</item>
/// <item><b><see cref="BuildRoute.Direct"/></b>: the sequence of <c>Player.PlacePiece</c> written by hand, for when
/// the owner's <c>Player</c> is not loaded here (a companion running on another machine) or for a test with no
/// player. Same steps, same order; the creator is the owner's id.</item>
/// </list>
///
/// <para><b>The executor does not validate and does not charge.</b> <c>PlacePiece</c> trusts the position, so the
/// caller must have a <c>valid</c> answer from <see cref="BuildProbe.CheckPoint"/> (E2 will do the full ghost
/// check); and the materials are paid by the caller (<see cref="Consume"/> / <see cref="Missing"/>), exactly like
/// the BuildOrders API does ("no building resources are consumed until the ghosts are built normally").</para>
///
/// <para>After placing, <see cref="Verify"/> answers the <i>Task Verification</i> of the plan: the object is alive,
/// has a valid ZDO, carries the piece, has the right creator, has health and - when it is a station - the game's
/// own registry sees it (<c>CraftingStation.HaveBuildStationInRange</c>). "The bench exists" and "the bench works"
/// are different questions.</para>
/// </summary>
internal static class BuildExecutor
{
	/// <summary>
	/// Places <paramref name="piece"/> at <paramref name="pos"/>/<paramref name="rot"/>. <paramref name="builder"/> is
	/// the companion (or null, for a test): it decides the creator and whether the player route can be used.
	/// </summary>
	internal static BuildResult Place(Component builder, Piece piece, Vector3 pos, Quaternion rot, bool forceDirect = false)
	{
		if (piece == null)
		{
			return BuildResult.Fail(BuildRoute.None, "no piece");
		}
		Player local = Player.m_localPlayer;
		long creator = CreatorOf(builder, local);
		bool playerRoute = !forceDirect && local != null && (builder == null || MHR_Owner.Of(builder) == local);
		if (playerRoute)
		{
			BuildResult viaPlayer = PlaceViaPlayer(local, piece, pos, rot);
			viaPlayer.Creator = creator;
			return viaPlayer;
		}
		return PlaceDirect(builder, piece, pos, rot, creator);
	}

	/// <summary>The creator id the piece will carry: the companion's owner, or the local player for a test.</summary>
	internal static long CreatorOf(Component builder, Player local)
	{
		if (builder != null)
		{
			long owner = MHR_Owner.CreatorId(builder);
			if (owner != 0L)
			{
				return owner;
			}
		}
		return local != null ? local.GetPlayerID() : 0L;
	}

	/// <summary>Route A: the game's own <c>Player.PlacePiece</c> (it is public and takes the pose, so no ghost is
	/// needed). <c>doAttack: false</c> skips the swing animation; the noise and the build stats stay vanilla.</summary>
	private static BuildResult PlaceViaPlayer(Player player, Piece piece, Vector3 pos, Quaternion rot)
	{
		BuildResult result = new BuildResult { Route = BuildRoute.PlayerPlacePiece };
		try
		{
			player.PlacePiece(piece, pos, rot, doAttack: false);
			result.Ok = true;
			result.Piece = piece;
			// PlacePiece returns void and instantiates inside; the verification (the caller's) is what proves it
			// worked. We still look for the new object to hand it back: the closest piece of that prefab to the pose.
			result.GameObject = FindPlaced(piece.gameObject.name, pos);
		}
		catch (Exception ex)
		{
			result.Ok = false;
			result.Error = "PlacePiece: " + ex.Message;
		}
		return result;
	}

	/// <summary>The sequence of <c>Player.PlacePiece</c>, by hand (decompiled 1.0.20, L3099). Every step matters:
	/// the claim before <c>SetCreator</c> (it only writes when the ZDO is ours and the creator is 0), the
	/// <c>IPlaced.OnPlaced</c> (it is what initialises a fire, a chest, a bed, a sign) and the ZDO check (a prefab
	/// that failed to wake up would leave a dead object in the world).</summary>
	private static BuildResult PlaceDirect(Component builder, Piece piece, Vector3 pos, Quaternion rot, long creator)
	{
		BuildResult result = new BuildResult { Route = BuildRoute.Direct, Creator = creator };
		GameObject go = null;
		TerrainModifier.SetTriggerOnPlaced(true);
		try
		{
			go = UnityEngine.Object.Instantiate(piece.gameObject, pos, rot);
		}
		catch (Exception ex)
		{
			result.Error = "Instantiate: " + ex.Message;
			return result;
		}
		finally
		{
			TerrainModifier.SetTriggerOnPlaced(false);
		}
		if (go == null)
		{
			result.Error = "Instantiate returned null";
			return result;
		}

		ZNetView nv = go.GetComponent<ZNetView>();
		if (nv == null || !nv.IsValid() || nv.GetZDO() == null)
		{
			// a prefab that did not wake up has no ZDO: keeping it would be a dead object in the world
			UnityEngine.Object.Destroy(go);
			result.Error = "the new object has no ZDO";
			return result;
		}
		if (!nv.IsOwner())
		{
			nv.ClaimOwnership();
		}
		Piece placed = go.GetComponent<Piece>();
		if (placed != null)
		{
			placed.SetCreator(creator, default(PlatformUserID));
			result.Piece = placed;
		}
		Player local = Player.m_localPlayer;
		PrivateArea area = go.GetComponent<PrivateArea>();
		if (area != null && local != null && Game.instance != null && Game.instance.GetPlayerProfile() != null)
		{
			area.Setup(Game.instance.GetPlayerProfile().GetName());
		}
		WearNTear wear = go.GetComponent<WearNTear>();
		if (wear != null)
		{
			wear.OnPlaced();
		}
		ItemDrop drop = go.GetComponent<ItemDrop>();
		if (drop != null)
		{
			drop.MakePiece(sendRPC: true);
		}
		// vanilla: gameObject.GetComponents(m_placed) + foreach (IPlaced item) item.OnPlaced()
		foreach (MonoBehaviour behaviour in go.GetComponents<MonoBehaviour>())
		{
			if (behaviour is IPlaced placedBehaviour)
			{
				try
				{
					placedBehaviour.OnPlaced();
				}
				catch (Exception ex)
				{
					CompanionsPlugin.Log?.LogWarning("[Build] IPlaced.OnPlaced falhou em " + go.name + ": " + ex.Message);
				}
			}
		}
		if (piece.m_placeEffect != null)
		{
			piece.m_placeEffect.Create(pos, rot, go.transform, 1f, -1, nv.GetZDO().m_uid);
		}
		if (builder is Character character)
		{
			// [Building] Quiet: the vanilla path (Player.PlacePiece) keeps its own noise; this one can be silenced
			if (ModConfig.BuildQuiet == null || !ModConfig.BuildQuiet.Value)
			{
				character.AddNoise(50f);
			}
		}
		result.Ok = true;
		result.GameObject = go;
		return result;
	}

	/// <summary>The closest object with that prefab name to the pose (route A gets no object back).</summary>
	private static GameObject FindPlaced(string prefabName, Vector3 pos)
	{
		GameObject best = null;
		float bestDistance = 1.5f;
		foreach (Piece candidate in UnityEngine.Object.FindObjectsByType<Piece>(FindObjectsSortMode.None))
		{
			if (candidate == null || candidate.gameObject.name != prefabName + "(Clone)")
			{
				continue;
			}
			float distance = Vector3.Distance(candidate.transform.position, pos);
			if (distance <= bestDistance)
			{
				bestDistance = distance;
				best = candidate.gameObject;
			}
		}
		return best;
	}

	/// <summary>
	/// The world's own answer: finds the placed piece near the pose and runs <see cref="Verify"/> on it. Used after a
	/// route-A placement (which returns no object) and by the job to re-check a piece that may have been built on
	/// another frame. <paramref name="found"/> is the object it judged (null when none was found).
	/// </summary>
	internal static string VerifyAt(Piece prefab, Vector3 pos, long expectedCreator, out GameObject found, bool checkStation = true)
	{
		found = null;
		if (prefab == null)
		{
			return "no_piece";
		}
		found = FindPlaced(prefab.gameObject.name, pos);
		return Verify(found, prefab, expectedCreator, checkStation);
	}

	/// <summary>
	/// Task Verification: null when the piece is really there and works, otherwise the reason. It checks the object,
	/// the ZDO, the piece component, the creator, the health and - for a station - the game's own registry, which is
	/// what makes the craft window open.
	///
	/// <para><b><paramref name="checkStation"/>:</b> <c>CraftingStation.Start()</c> is what adds the station to
	/// <c>m_allStations</c>, and Unity runs <c>Start</c> on the <b>next frame</b> after the instantiate. Checking the
	/// registry in the same frame as the placement always says "not registered" (measured in the game, 01/11/2026),
	/// so the immediate check passes <c>false</c> and the deferred one (the job, or the test's next-frame check)
	/// passes <c>true</c>.</para>
	/// </summary>
	internal static string Verify(GameObject go, Piece piece, long expectedCreator, bool checkStation = true)
	{
		if (go == null)
		{
			return "not_found";
		}
		if (!go.activeInHierarchy)
		{
			return "inactive";
		}
		ZNetView nv = go.GetComponent<ZNetView>();
		if (nv == null || !nv.IsValid() || nv.GetZDO() == null)
		{
			return "no_zdo";
		}
		Piece placed = go.GetComponent<Piece>();
		if (placed == null)
		{
			return "no_piece";
		}
		if (placed.GetCreator() == 0L)
		{
			return "no_creator";
		}
		if (expectedCreator != 0L && placed.GetCreator() != expectedCreator)
		{
			return "wrong_creator";
		}
		WearNTear wear = go.GetComponent<WearNTear>();
		if (wear != null && wear.GetHealthPercentage() <= 0f)
		{
			return "no_health";
		}
		if (checkStation)
		{
			CraftingStation station = go.GetComponentInChildren<CraftingStation>();
			if (station != null && CraftingStation.HaveBuildStationInRange(station.m_name, go.transform.position) == null)
			{
				return "station_not_registered";
			}
		}
		return null;
	}

	/// <summary>What the piece still needs, as item shared name → amount (empty when the inventory covers it).</summary>
	internal static Dictionary<string, int> Missing(Inventory inventory, Piece piece)
	{
		Dictionary<string, int> missing = new Dictionary<string, int>();
		if (inventory == null || piece == null || piece.m_resources == null)
		{
			return missing;
		}
		foreach (Piece.Requirement requirement in piece.m_resources)
		{
			if (requirement == null || requirement.m_resItem == null)
			{
				continue;
			}
			string name = requirement.m_resItem.m_itemData.m_shared.m_name;
			int need = requirement.GetAmount(1);
			if (need <= 0)
			{
				continue;
			}
			int have = inventory.CountItems(name);
			if (have < need)
			{
				missing[name] = (missing.TryGetValue(name, out int already) ? already : 0) + (need - have);
			}
		}
		return missing;
	}

	/// <summary>
	/// All-or-nothing: pays the piece's <c>m_resources</c> from the inventory, or takes nothing. The cost is always
	/// the game's (<see cref="Piece.Requirement.GetAmount"/>), never a table of ours.
	/// </summary>
	internal static bool Consume(Inventory inventory, Piece piece)
	{
		Dictionary<string, int> missing = Missing(inventory, piece);
		if (missing.Count > 0)
		{
			return false;
		}
		foreach (Piece.Requirement requirement in piece.m_resources)
		{
			if (requirement == null || requirement.m_resItem == null)
			{
				continue;
			}
			int need = requirement.GetAmount(1);
			if (need > 0)
			{
				inventory.RemoveItem(requirement.m_resItem.m_itemData.m_shared.m_name, need);
			}
		}
		return true;
	}

	/// <summary>The cost as text ("Madeira x10, Pedra x5"), for the log and the refusal line.</summary>
	internal static string CostText(Piece piece)
	{
		if (piece == null || piece.m_resources == null || piece.m_resources.Length == 0)
		{
			return "(nenhum)";
		}
		List<string> parts = new List<string>();
		foreach (Piece.Requirement requirement in piece.m_resources)
		{
			if (requirement == null || requirement.m_resItem == null)
			{
				continue;
			}
			string name = Localization.instance != null ? Localization.instance.Localize(requirement.m_resItem.m_itemData.m_shared.m_name) : requirement.m_resItem.m_itemData.m_shared.m_name;
			parts.Add(name + " x" + requirement.GetAmount(1));
		}
		return string.Join(", ", parts.ToArray());
	}
}
