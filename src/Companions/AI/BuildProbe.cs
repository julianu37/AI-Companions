using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Companions;

/// <summary>
/// Fase E0 da construção autônoma: a <b>sonda</b>. It reads a buildable piece from the live world and answers the
/// two questions the rest of the feature depends on, <b>without placing anything</b>:
///
/// <list type="bullet">
/// <item>what the piece <i>is</i> - recipe, station, flags, colliders' support - read from the game's own prefab
/// (the flags are not published anywhere: they live in the asset bundle);</item>
/// <item>whether a point would accept it - the checks of <c>Player.UpdatePlacementGhost</c> that do not need the
/// ghost (ground, water, biome, ward, no-build zone, station in range, space, players), each with a stable reason
/// name.</item>
/// </list>
///
/// <para>It is used by the <c>modia_build_probe</c> console command and by the DevBridge's <c>build.inspect</c> /
/// <c>build.sites</c>, so the agent and the player see the same data. The real validator (E2,
/// <see cref="BuildSite"/>) grows out of <see cref="CheckPoint"/> - the same reason names, so the log of a test is
/// already the vocabulary of the feature.</para>
///
/// <para>Every member runs on the Unity main thread and is null-safe outside a world (the DevBridge answers with an
/// error instead of throwing). Nothing here allocates per frame: it is called by hand, once.</para>
/// </summary>
internal static class BuildProbe
{
	/// <summary>Players and companions keep this distance from a candidate spot. It is a cheap "not inside a body"
	/// guard, not the vanilla rule: the game tests the ghost's colliders against every character within 30 m
	/// (<c>CheckPlacementGhostVSPlayers</c>, <c>Player.cs</c> L4195) and a bench 1 m in front of you is normal. The
	/// collider test is what E2 adds; 1,5 m here would refuse the obvious spot (measured: the test at 1,5 m in front
	/// of the player failed with <c>blocked_by_player</c>).</summary>
	internal const float MinSpacing = 0.6f;

	private const float ProbeAbove = 4f;
	private const float ProbeBelow = 8f;
	/// <summary>
	/// P1 (04/11/2026, measured on the 985-piece house): how close to the pose a piece of the structure has to be for the
	/// probe to accept a pose whose vertical ray found nothing. The ridge at y 47,8 had roofs/beams around it while the ray
	/// went through the gap between the two slopes; the game accepts the piece (its own <c>WearNTear</c> support verdict is
	/// what decides), so the probe must not refuse it for a ray that missed.
	///
	/// <para><b>4,0 m and not 2,6 m</b> (measured in the run of 04/11/2026: a 2,6 m sphere found nothing and the ridge stayed
	/// <c>no_ground</c>): the roof grid is ~2 m and the nearest neighbour of a <c>wood_roof_45</c> sits on the DIAGONAL, ~2,8
	/// to 3,5 m away. Only runs when the vertical ray missed (rare), so the cost is one overlap sphere on those poses.</para>
	/// </summary>
	private const float AttachedRadius = 4f;

	private static int _placeMask = -1;
	private static int _waterMask = -1;
	private static int _waterLayer = -2;
	private static int _pieceMask = -1;

	/// <summary>The layers the vanilla placement ray looks through (<c>Player.m_placeWaterRayMask</c>, decompiled
	/// 1.0.20): the first hit tells us ground, piece or water.</summary>
	internal static int PlaceMask()
	{
		if (_placeMask < 0)
		{
			_placeMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "terrain", "vehicle", "Water");
		}
		return _placeMask;
	}

	private static int WaterMask()
	{
		if (_waterMask < 0)
		{
			_waterMask = LayerMask.GetMask("Water");
		}
		return _waterMask;
	}

	private static int WaterLayer()
	{
		if (_waterLayer == -2)
		{
			_waterLayer = LayerMask.NameToLayer("Water");
		}
		return _waterLayer;
	}

	private static int PieceMask()
	{
		if (_pieceMask < 0)
		{
			_pieceMask = LayerMask.GetMask("piece", "piece_nonsolid");
		}
		return _pieceMask;
	}

	/// <summary>The piece prefab by name, or null (also null when the prefab exists but is not a buildable piece).</summary>
	internal static Piece PieceOf(string prefab)
	{
		if (string.IsNullOrEmpty(prefab) || ZNetScene.instance == null)
		{
			return null;
		}
		string name = prefab.Trim();
		GameObject go = ZNetScene.instance.GetPrefab(name);
		if (go == null && ZNetScene.instance.m_prefabs != null)
		{
			// E7: a .blueprint may spell the prefab with another case ("Piece_grausten_floor_2x2" x
			// "piece_grausten_floor_2x2" - the BiomeBlueprints files do) and GetPrefab is an exact lookup
			foreach (GameObject candidate in ZNetScene.instance.m_prefabs)
			{
				if (candidate != null && string.Equals(candidate.name, name, StringComparison.OrdinalIgnoreCase))
				{
					go = candidate;
					break;
				}
			}
		}
		return go != null ? go.GetComponent<Piece>() : null;
	}

	private static string Loc(string token)
	{
		if (string.IsNullOrEmpty(token))
		{
			return token;
		}
		return Localization.instance != null ? Localization.instance.Localize(token) : token;
	}

	/// <summary>
	/// Everything the mod needs to know about a piece: the recipe (<c>m_resources</c>), the station it needs, the
	/// placement flags, the space/connection rules and the structural bits. The keys are stable - the notes in
	/// <c>docs/re/building-notes.md</c> and the tests read them by name.
	/// </summary>
	internal static Dictionary<string, object> Describe(string prefab)
	{
		Dictionary<string, object> result = new Dictionary<string, object>();
		result["prefab"] = prefab ?? string.Empty;
		Piece piece = PieceOf(prefab);
		if (piece == null)
		{
			result["found"] = false;
			result["reason"] = ZNetScene.instance == null ? "the world isn't loaded" : "no Piece component on that prefab";
			return result;
		}
		result["found"] = true;
		result["name"] = Loc(piece.m_name);
		result["rawName"] = piece.m_name;
		result["description"] = Loc(piece.m_description);
		result["inHammer"] = InHammer(piece);
		Player player = Player.m_localPlayer;
		result["recipeKnown"] = player != null && player.IsRecipeKnown(piece.m_name);

		// recipe: the cost the feature will charge, straight from the game
		List<object> resources = new List<object>();
		if (piece.m_resources != null)
		{
			foreach (Piece.Requirement req in piece.m_resources)
			{
				if (req == null || req.m_resItem == null)
				{
					continue;
				}
				resources.Add(new Dictionary<string, object>
				{
					{ "item", req.m_resItem.gameObject.name },
					{ "name", Loc(req.m_resItem.m_itemData.m_shared.m_name) },
					{ "rawName", req.m_resItem.m_itemData.m_shared.m_name },
					{ "amount", req.m_amount },
					{ "perLevel", req.m_amountPerLevel },
					{ "recover", req.m_recover }
				});
			}
		}
		result["resources"] = resources;

		CraftingStation station = piece.m_craftingStation;
		if (station != null)
		{
			Dictionary<string, object> stationData = new Dictionary<string, object>
			{
				{ "name", Loc(station.m_name) },
				{ "rawName", station.m_name },
				{ "rangeBuild", station.m_rangeBuild },
				{ "discoverRange", station.m_discoverRange }
			};
			if (player != null)
			{
				stationData["inRangeAtPlayer"] = CraftingStation.HaveBuildStationInRange(station.m_name, player.transform.position) != null;
			}
			result["station"] = stationData;
		}

		result["flags"] = new Dictionary<string, object>
		{
			{ "groundOnly", piece.m_groundOnly },
			{ "groundPiece", piece.m_groundPiece },
			{ "cultivatedGroundOnly", piece.m_cultivatedGroundOnly },
			{ "vegetationGroundOnly", piece.m_vegetationGroundOnly },
			{ "waterPiece", piece.m_waterPiece },
			{ "noInWater", piece.m_noInWater },
			{ "clipGround", piece.m_clipGround },
			{ "clipEverything", piece.m_clipEverything },
			{ "notOnWood", piece.m_notOnWood },
			{ "notOnTiltingSurface", piece.m_notOnTiltingSurface },
			{ "inCeilingOnly", piece.m_inCeilingOnly },
			{ "notOnFloor", piece.m_notOnFloor },
			{ "noClipping", piece.m_noClipping },
			{ "onlyInTeleportArea", piece.m_onlyInTeleportArea },
			{ "allowedInDungeons", piece.m_allowedInDungeons },
			{ "allowedInDeepSnow", piece.m_allowedInDeepSnow },
			{ "requireDeepSnow", piece.m_requireDeepSnow },
			{ "allowAltGroundPlacement", piece.m_allowAltGroundPlacement }
		};

		List<object> blocking = new List<object>();
		if (piece.m_blockingPieces != null)
		{
			foreach (Piece other in piece.m_blockingPieces)
			{
				if (other != null)
				{
					blocking.Add(other.gameObject.name);
				}
			}
		}
		result["space"] = new Dictionary<string, object>
		{
			{ "spaceRequirement", piece.m_spaceRequirement },
			{ "blockRadius", piece.m_blockRadius },
			{ "blockingPieces", blocking },
			{ "mustConnectTo", piece.m_mustConnectTo != null ? piece.m_mustConnectTo.gameObject.name : null },
			{ "connectRadius", piece.m_connectRadius },
			{ "onlyInBiome", piece.m_onlyInBiome.ToString() },
			{ "canRotate", piece.m_canRotate },
			{ "canBeRemoved", piece.m_canBeRemoved },
			{ "allowRotatedOverlap", piece.m_allowRotatedOverlap },
			{ "randomInitBuildRotation", piece.m_randomInitBuildRotation },
			{ "extraPlacementDistance", piece.m_extraPlacementDistance },
			{ "comfort", piece.m_comfort },
			{ "comfortGroup", piece.m_comfortGroup.ToString() }
		};

		WearNTear wear = piece.GetComponent<WearNTear>();
		if (wear != null)
		{
			result["wearNTear"] = new Dictionary<string, object>
			{
				{ "supports", wear.m_supports },
				{ "material", wear.m_materialType.ToString() },
				{ "health", wear.m_health },
				{ "noSupportWear", wear.m_noSupportWear },
				{ "noRoofWear", wear.m_noRoofWear }
			};
		}

		CraftingStation selfStation = piece.GetComponentInChildren<CraftingStation>();
		if (selfStation != null)
		{
			result["craftingStation"] = new Dictionary<string, object>
			{
				{ "name", Loc(selfStation.m_name) },
				{ "rawName", selfStation.m_name },
				{ "rangeBuild", selfStation.m_rangeBuild },
				{ "useDistance", selfStation.m_useDistance },
				{ "requireRoof", selfStation.m_craftRequireRoof },
				{ "requireFire", selfStation.m_craftRequireFire }
			};
		}

		PrivateArea area = piece.GetComponent<PrivateArea>();
		if (area != null)
		{
			result["privateArea"] = new Dictionary<string, object> { { "radius", area.m_radius } };
		}

		StationExtension extension = piece.GetComponent<StationExtension>();
		if (extension != null)
		{
			result["stationExtension"] = true;
		}

		result["itemDrop"] = piece.GetComponent<ItemDrop>() != null;
		result["placeEffect"] = piece.m_placeEffect != null && piece.m_placeEffect.m_effectPrefabs != null && piece.m_placeEffect.m_effectPrefabs.Length > 0;
		result["bounds"] = Bounds(piece);
		return result;
	}

	/// <summary>
	/// E6: how big the piece is and where its box sits relative to the prefab's origin - the numbers the templates
	/// are written with (a floor is 2x2, a wall is 2 wide x 2 high and 0,2 thick, and the origin is at the base).
	/// It reads the renderers of the prefab (the meshes are there even with the prefab inactive) and falls back to
	/// the colliders when a piece has no renderer.
	/// </summary>
	internal static Dictionary<string, object> Bounds(Piece piece)
	{
		Dictionary<string, object> result = new Dictionary<string, object>();
		if (piece == null)
		{
			return result;
		}
		try
		{
			Bounds box;
			if (!BoxOf(piece, out box))
			{
				result["known"] = false;
				return result;
			}
			Vector3 origin = piece.transform.position;
			result["known"] = true;
			result["size"] = new Dictionary<string, object> { { "x", box.size.x }, { "y", box.size.y }, { "z", box.size.z } };
			result["center"] = new Dictionary<string, object>
			{
				{ "x", box.center.x - origin.x },
				{ "y", box.center.y - origin.y },
				{ "z", box.center.z - origin.z }
			};
			result["min"] = new Dictionary<string, object>
			{
				{ "x", box.min.x - origin.x },
				{ "y", box.min.y - origin.y },
				{ "z", box.min.z - origin.z }
			};
			result["max"] = new Dictionary<string, object>
			{
				{ "x", box.max.x - origin.x },
				{ "y", box.max.y - origin.y },
				{ "z", box.max.z - origin.z }
			};
		}
		catch (Exception ex)
		{
			result["known"] = false;
			result["error"] = ex.Message;
		}
		return result;
	}

	/// <summary>
	/// The piece's box in the world, from its renderers (the meshes are there even with the prefab inactive) and,
	/// when it has none, from its colliders. It is what <see cref="Bounds"/> reports and what the E6 footprint
	/// support test uses.
	/// </summary>
	internal static bool BoxOf(Piece piece, out Bounds box)
	{
		box = new Bounds();
		if (piece == null)
		{
			return false;
		}
		bool any = false;
		Renderer[] renderers = piece.GetComponentsInChildren<Renderer>(true);
		foreach (Renderer renderer in renderers)
		{
			// the effect renderers of a prefab have zero bounds and would drag the box to the origin
			if (renderer == null || renderer is LineRenderer || renderer is TrailRenderer || renderer.bounds.size.sqrMagnitude < 0.0001f)
			{
				continue;
			}
			if (!any)
			{
				box = renderer.bounds;
				any = true;
			}
			else
			{
				box.Encapsulate(renderer.bounds);
			}
		}
		if (!any)
		{
			Collider[] colliders = piece.GetComponentsInChildren<Collider>(true);
			foreach (Collider collider in colliders)
			{
				if (collider == null || collider.isTrigger)
				{
					continue;
				}
				if (!any)
				{
					box = collider.bounds;
					any = true;
				}
				else
				{
					box.Encapsulate(collider.bounds);
				}
			}
		}
		return any;
	}

	/// <summary>Is the piece in the player's hammer table? (the authoritative "you can build this" list, mods
	/// included - the same table PlanBuild and the hammer scan).</summary>
	private static bool InHammer(Piece piece)
	{
		try
		{
			PieceTable table = ZNetScene.instance?.GetPrefab("Hammer")?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_buildPieces;
			if (table == null || table.m_pieces == null)
			{
				return false;
			}
			foreach (GameObject go in table.m_pieces)
			{
				if (go != null && go.GetComponent<Piece>() == piece)
				{
					return true;
				}
			}
		}
		catch (Exception)
		{
			// a broken custom piece table must not break the probe
		}
		return false;
	}

	/// <summary>
	/// Would this point accept the piece? Runs the checks of <c>Player.UpdatePlacementGhost</c> that do not need the
	/// ghost itself, in the same order, and returns every reason it found (the first one is the status). The names
	/// are stable and are the vocabulary of the feature: <c>valid</c>, <c>no_ground</c>, <c>in_water</c>,
	/// <c>no_water</c>, <c>need_cultivated</c>, <c>need_dirt</c>, <c>not_on_wood</c>, <c>too_steep</c>,
	/// <c>not_ceiling</c>, <c>not_on_floor</c>, <c>no_teleport_area</c>, <c>not_in_dungeon</c>, <c>deep_snow</c>,
	/// <c>no_snow</c>, <c>wrong_biome</c>, <c>no_build_zone</c>, <c>ward</c>, <c>needs_station</c>,
	/// <c>too_close_piece</c>, <c>blocked_by_player</c>, <c>no_support</c>.
	/// </summary>
	internal static Dictionary<string, object> CheckPoint(Piece piece, Vector3 point)
	{
		return CheckPoint(piece, point, Quaternion.identity);
	}

	/// <summary>
	/// The same check for a piece at a <b>known rotation</b> (E6): a template places walls and roofs with a yaw, and
	/// the footprint support test has to use the turned box.
	/// </summary>
	internal static Dictionary<string, object> CheckPoint(Piece piece, Vector3 point, Quaternion rot)
	{
		return CheckPoint(piece, point, rot, null);
	}

	/// <summary>
	/// The same check with a <b>character to ignore</b> (E6): the companion that is placing the piece must not count as
	/// an obstacle for its own piece - it walks up to it, so it is always inside the spacing radius. The player and the
	/// other companions still count.
	/// </summary>
	internal static Dictionary<string, object> CheckPoint(Piece piece, Vector3 point, Quaternion rot, Component ignore)
	{
		Dictionary<string, object> result = new Dictionary<string, object>();
		result["x"] = point.x;
		result["y"] = point.y;
		result["z"] = point.z;
		List<string> reasons = new List<string>();
		if (piece == null)
		{
			reasons.Add("no_piece");
			result["ok"] = false;
			result["status"] = "no_piece";
			result["reasons"] = reasons;
			return result;
		}

		Vector3 from = point + Vector3.up * ProbeAbove;
		RaycastHit hit;
		bool hasHit = Physics.Raycast(from, Vector3.down, out hit, ProbeAbove + ProbeBelow, PlaceMask(), QueryTriggerInteraction.Ignore);
		Vector3 surface = hasHit ? hit.point : point;
		bool water = hasHit && WaterLayer() >= 0 && hit.collider.gameObject.layer == WaterLayer();
		Heightmap heightmap = hasHit ? hit.collider.GetComponent<Heightmap>() : null;
		Piece groundPiece = hasHit ? hit.collider.GetComponentInParent<Piece>() : null;
		WearNTear groundWear = groundPiece != null ? groundPiece.GetComponent<WearNTear>() : null;
		Heightmap.Biome biome = Heightmap.FindBiome(surface);

		if (!hasHit && !AttachedToStructure(point))
		{
			// P1 (04/11/2026, measured): the vertical ray of the probe goes through the GAP between two roof slopes - the ridge
			// at y 47,8 had roof/beam pieces within 1 m and the ray found nothing, so every ridge piece was refused with
			// no_ground and the game would have accepted it. Touching the structure is enough here: the support itself is the
			// game's own WearNTear verdict, checked when the piece is placed (Place/Verify).
			reasons.Add("no_ground");
		}
		if (piece.m_noInWater && water)
		{
			reasons.Add("in_water");
		}
		if (piece.m_waterPiece && !water)
		{
			reasons.Add("no_water");
		}
		if ((piece.m_groundOnly || piece.m_groundPiece) && heightmap == null)
		{
			reasons.Add("no_ground");
		}
		if (piece.m_cultivatedGroundOnly && (heightmap == null || !heightmap.IsCultivated(surface)))
		{
			reasons.Add("need_cultivated");
		}
		if (piece.m_vegetationGroundOnly)
		{
			bool bad = heightmap == null;
			if (!bad)
			{
				float mask = heightmap.GetVegetationMask(surface);
				bad = biome == Heightmap.Biome.AshLands ? mask > 0.1f : mask < 0.25f;
			}
			if (bad)
			{
				reasons.Add("need_dirt");
			}
		}
		if (piece.m_notOnWood && groundWear != null && (groundWear.m_materialType == WearNTear.MaterialType.Wood || groundWear.m_materialType == WearNTear.MaterialType.HardWood))
		{
			reasons.Add("not_on_wood");
		}
		float normalY = hasHit ? hit.normal.y : 1f;
		if (piece.m_notOnTiltingSurface && normalY < 0.8f)
		{
			reasons.Add("too_steep");
		}
		if (piece.m_inCeilingOnly && normalY > -0.5f)
		{
			reasons.Add("not_ceiling");
		}
		if (piece.m_notOnFloor && normalY > 0.1f)
		{
			reasons.Add("not_on_floor");
		}
		if (piece.m_onlyInTeleportArea && EffectArea.IsPointInsideArea(surface, EffectArea.Type.Teleport) == null)
		{
			reasons.Add("no_teleport_area");
		}
		if (!piece.m_allowedInDungeons && Character.InInterior(surface))
		{
			reasons.Add("not_in_dungeon");
		}
		float snowMask = heightmap != null ? heightmap.GetCultivationMask(surface) : 0f;
		if (!piece.m_allowedInDeepSnow && biome == Heightmap.Biome.DeepNorth && snowMask > 0.15f)
		{
			reasons.Add("deep_snow");
		}
		if (piece.m_requireDeepSnow && (biome != Heightmap.Biome.DeepNorth || snowMask <= 0f))
		{
			reasons.Add("no_snow");
		}
		if (piece.m_onlyInBiome != Heightmap.Biome.None && (biome & piece.m_onlyInBiome) == 0)
		{
			reasons.Add("wrong_biome");
		}
		if (Location.IsInsideNoBuildLocation(surface))
		{
			reasons.Add("no_build_zone");
		}
		float wardRadius = piece.GetComponent<PrivateArea>() != null ? piece.GetComponent<PrivateArea>().m_radius : 0f;
		if (!PrivateArea.CheckAccess(surface, wardRadius, false, false))
		{
			reasons.Add("ward");
		}
		if (piece.m_craftingStation != null && CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name, surface) == null)
		{
			reasons.Add("needs_station");
		}
		if (piece.m_blockRadius > 0f && piece.m_blockingPieces != null && piece.m_blockingPieces.Count > 0)
		{
			Collider[] near = Physics.OverlapSphere(surface, piece.m_blockRadius, PieceMask());
			for (int i = 0; i < near.Length; i++)
			{
				Piece other = near[i] != null ? near[i].GetComponentInParent<Piece>() : null;
				if (other == null || other == piece)
				{
					continue;
				}
				foreach (Piece blocked in piece.m_blockingPieces)
				{
					if (blocked != null && blocked.m_name == other.m_name)
					{
						reasons.Add("too_close_piece");
						i = near.Length;
						break;
					}
				}
			}
		}
		if (BlockedByCharacter(surface, ignore))
		{
			reasons.Add("blocked_by_player");
		}
		// Vanilla reads the WearNTear of the piece the ghost is STANDING ON (the hit's Piece), not the ghost's own:
		// "if (wearNTear && !wearNTear.m_supports) status = Invalid" - that is what stops a piece being put on top of
		// a workbench (furniture does not carry support). The probe got this wrong on the first run and every point
		// came back no_support.
		// E6: for a piece ABOVE the ground (a wall on a floor, a roof on a wall) the piece under its CENTRE can be
		// furniture that does not support while the piece is really held by walls/poles at its edges - so up there the
		// test is the footprint, the same idea the game's own UpdateSupport uses (the collider boxes grown by 0,15 m
		// looking for the ground or for a supporting piece).
		bool elevated = hasHit && point.y - surface.y > 0.6f;
		if (elevated)
		{
			if (!FootprintSupported(piece, point, rot))
			{
				reasons.Add("no_support");
			}
		}
		else if (groundWear != null && !groundWear.m_supports)
		{
			reasons.Add("no_support");
		}

		result["ok"] = reasons.Count == 0;
		result["status"] = reasons.Count == 0 ? "valid" : reasons[0];
		result["elevated"] = elevated;
		result["reasons"] = reasons;
		result["ground"] = new Dictionary<string, object>
		{
			{ "hit", hasHit },
			{ "name", hasHit && hit.collider != null ? hit.collider.gameObject.name : null },
			{ "isHeightmap", heightmap != null },
			{ "isPiece", groundPiece != null },
			{ "piece", groundPiece != null ? Loc(groundPiece.m_name) : null },
			{ "wearSupports", groundWear != null ? (object)groundWear.m_supports : null },
			{ "isWater", water },
			{ "normalY", normalY },
			{ "drop", point.y - surface.y }
		};
		result["biome"] = biome.ToString();
		result["cultivated"] = heightmap != null && heightmap.IsCultivated(surface);
		result["vegetation"] = heightmap != null ? heightmap.GetVegetationMask(surface) : 0f;
		result["inWater"] = water;
		result["hasStation"] = piece.m_craftingStation == null || CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name, surface) != null;
		result["wardOk"] = PrivateArea.CheckAccess(surface, wardRadius, false, false);
		result["noBuildZone"] = Location.IsInsideNoBuildLocation(surface);
		result["groundHeight"] = ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(surface) : surface.y;
		return result;
	}

	/// <summary>
	/// P1 (04/11/2026, measured on the 985-piece house): is the pose <b>touching the structure</b> even though the vertical
	/// probe ray found nothing under it? The ridge of a roof sits over the gap between the two slopes: the ray misses while
	/// the pieces that hold it (roof, beam, gable top) are within a metre. Any collider of a <see cref="Piece"/> or a
	/// <see cref="WearNTear"/> inside <see cref="AttachedRadius"/> answers yes, and then the probe does not call it
	/// <c>no_ground</c> - the support rule is the game's own verdict at placement. It only runs when the ray missed (rare),
	/// so the cost is one overlap sphere on those poses.
	/// </summary>
	private static bool AttachedToStructure(Vector3 point)
	{
		Collider[] near = Physics.OverlapSphere(point, AttachedRadius, PlaceMask(), QueryTriggerInteraction.Ignore);
		for (int i = 0; i < near.Length; i++)
		{
			Collider collider = near[i];
			if (collider == null || collider.isTrigger)
			{
				continue;
			}
			if (collider.GetComponentInParent<Piece>() != null || collider.GetComponentInParent<WearNTear>() != null)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// E6: is there the ground (the terrain) or a supporting piece inside the piece's footprint, grown by 0,15 m? It
	/// is the elevated-piece half of the <c>no_support</c> rule - the same reach the game's <c>UpdateSupport</c> uses.
	/// No box to test (an exotic prefab) answers <c>true</c>: never a refusal for a missing measurement.
	/// </summary>
	private static bool FootprintSupported(Piece piece, Vector3 pos, Quaternion rot)
	{
		Bounds box;
		if (!BoxOf(piece, out box))
		{
			return true;
		}
		Vector3 localCenter = box.center - piece.transform.position;
		Vector3 center = pos + rot * localCenter;
		Vector3 half = box.extents + new Vector3(0.15f, 0.15f, 0.15f);
		int terrain = LayerMask.NameToLayer("terrain");
		Collider[] near = Physics.OverlapBox(center, half, rot, PlaceMask(), QueryTriggerInteraction.Ignore);
		foreach (Collider collider in near)
		{
			if (collider == null || !collider.enabled || collider.isTrigger)
			{
				continue;
			}
			if (terrain >= 0 && collider.gameObject.layer == terrain)
			{
				return true;
			}
			WearNTear wear = collider.GetComponentInParent<WearNTear>();
			if (wear != null && wear.m_supports)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// A player inside <see cref="MinSpacing"/> of the spot (the vanilla ghost check, cheap version: the game tests the ghost's
	/// colliders against every character within 30 m).
	///
	/// <para>P1 (04/11/2026, measured: <b>17+</b> <c>blocked_by_player</c> in a run of 3 companions on the same site): a
	/// <b>companion is not a player</b> for placement - the game pushes an NPC out of the way instead of refusing the piece,
	/// and the workers stand right next to the piece they are building. Any character that has a
	/// <see cref="CompanionAI"/> is ignored (the one placing the piece is already the caller's
	/// <paramref name="ignore"/>; the others must not block it). The test against a real player is unchanged.</para>
	/// </summary>
	private static bool BlockedByCharacter(Vector3 point, Component ignore)
	{
		float sqr = MinSpacing * MinSpacing;
		foreach (Player player in Player.GetAllPlayers())
		{
			if (player == null || ReferenceEquals(player, ignore))
			{
				continue;
			}
			if ((player.transform.position - point).sqrMagnitude < sqr)
			{
				return true;
			}
		}
		foreach (CompanionSetup setup in CompanionSetup.AllCompanions)
		{
			if (setup == null || ReferenceEquals(setup, ignore))
			{
				continue;
			}
			if ((setup.transform.position - point).sqrMagnitude < sqr)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Tests a ring of candidate spots around a centre: <paramref name="rings"/> distances out to
	/// <paramref name="radius"/>, <paramref name="dirs"/> directions each. The ground height comes from
	/// <c>ZoneSystem.GetGroundHeight</c> so every point is tested at the height the game would use.
	/// </summary>
	internal static List<object> Scan(Piece piece, Vector3 center, float radius, int rings, int dirs)
	{
		List<object> points = new List<object>();
		if (piece == null || rings <= 0 || dirs <= 0)
		{
			return points;
		}
		rings = Mathf.Clamp(rings, 1, 8);
		dirs = Mathf.Clamp(dirs, 1, 24);
		radius = Mathf.Clamp(radius, 0.5f, 40f);
		for (int ring = 0; ring < rings; ring++)
		{
			float distance = radius * (ring + 1) / rings;
			for (int dir = 0; dir < dirs; dir++)
			{
				float angle = Mathf.PI * 2f * dir / dirs;
				Vector3 flat = center + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
				float y = ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(flat) : center.y;
				Dictionary<string, object> check = CheckPoint(piece, new Vector3(flat.x, y, flat.z));
				check["ring"] = ring + 1;
				check["dir"] = dir;
				check["distance"] = distance;
				points.Add(check);
			}
		}
		return points;
	}

	// ------------------------------------------------------------------ text form (console command)

	/// <summary>The <see cref="Describe"/> answer as the lines the console command prints (the same numbers the
	/// DevBridge returns as JSON).</summary>
	internal static string DescribeText(string prefab)
	{
		Dictionary<string, object> d = Describe(prefab);
		StringBuilder sb = new StringBuilder();
		if (!(d.TryGetValue("found", out object found) && found is bool ok && ok))
		{
			sb.Append("[BuildProbe] ").Append(prefab).Append(": ").Append(d["reason"]);
			return sb.ToString();
		}
		sb.Append("[BuildProbe] ").Append(prefab).Append(" | ").Append(d["name"]).Append(" | hammer=").Append(Yes(d["inHammer"]))
			.Append(" recipeKnown=").Append(Yes(d["recipeKnown"])).Append(" itemDrop=").Append(Yes(d["itemDrop"]));
		if (d.ContainsKey("station"))
		{
			Dictionary<string, object> st = (Dictionary<string, object>)d["station"];
			sb.Append(" | station=").Append(st["name"]).Append(" (range ").Append(st["rangeBuild"]).Append(')');
		}
		sb.AppendLine();

		sb.Append("[BuildProbe] custo: ");
		List<object> res = (List<object>)d["resources"];
		if (res.Count == 0)
		{
			sb.Append("(nenhum)");
		}
		for (int i = 0; i < res.Count; i++)
		{
			Dictionary<string, object> r = (Dictionary<string, object>)res[i];
			if (i > 0)
			{
				sb.Append(", ");
			}
			sb.Append(r["name"]).Append(" x").Append(r["amount"]);
		}
		sb.AppendLine();

		Dictionary<string, object> f = (Dictionary<string, object>)d["flags"];
		sb.Append("[BuildProbe] flags: ");
		bool first = true;
		foreach (KeyValuePair<string, object> kv in f)
		{
			if (kv.Value is bool b && !b)
			{
				continue;
			}
			if (!first)
			{
				sb.Append(' ');
			}
			first = false;
			sb.Append(kv.Key).Append('=').Append(Yes(kv.Value));
		}
		if (first)
		{
			sb.Append("(todas falsas)");
		}
		sb.AppendLine();

		Dictionary<string, object> sp = (Dictionary<string, object>)d["space"];
		sb.Append("[BuildProbe] espaco: spaceReq=").Append(sp["spaceRequirement"]).Append(" blockRadius=").Append(sp["blockRadius"])
			.Append(" blocking=").Append(ListText(sp["blockingPieces"]))
			.Append(" connect=").Append(sp["mustConnectTo"] ?? "(nenhum)")
			.Append(" biome=").Append(sp["onlyInBiome"])
			.Append(" rotate=").Append(Yes(sp["canRotate"]))
			.Append(" remove=").Append(Yes(sp["canBeRemoved"]));
		sb.AppendLine();

		if (d.ContainsKey("wearNTear"))
		{
			Dictionary<string, object> w = (Dictionary<string, object>)d["wearNTear"];
			sb.Append("[BuildProbe] wear: supports=").Append(Yes(w["supports"])).Append(" material=").Append(w["material"])
				.Append(" health=").Append(w["health"]).Append(" noSupportWear=").Append(Yes(w["noSupportWear"]));
			sb.AppendLine();
		}
		if (d.ContainsKey("craftingStation"))
		{
			Dictionary<string, object> cs = (Dictionary<string, object>)d["craftingStation"];
			sb.Append("[BuildProbe] estacao: ").Append(cs["name"]).Append(" rangeBuild=").Append(cs["rangeBuild"])
				.Append(" requireRoof=").Append(Yes(cs["requireRoof"])).Append(" requireFire=").Append(Yes(cs["requireFire"]));
			sb.AppendLine();
		}
		if (d.ContainsKey("privateArea"))
		{
			sb.Append("[BuildProbe] ward: radius=").Append(((Dictionary<string, object>)d["privateArea"])["radius"]).AppendLine();
		}
		return sb.ToString().TrimEnd();
	}

	/// <summary>The <see cref="Scan"/> answer as text: one line per point, the valid ones marked.</summary>
	internal static string ScanText(Piece piece, Vector3 center, float radius, int rings, int dirs)
	{
		List<object> points = Scan(piece, center, radius, rings, dirs);
		StringBuilder sb = new StringBuilder();
		int valid = 0;
		foreach (object entry in points)
		{
			Dictionary<string, object> p = (Dictionary<string, object>)entry;
			if (p["ok"] is bool ok && ok)
			{
				valid++;
			}
		}
		sb.Append("[BuildProbe] ").Append(points.Count).Append(" pontos em volta de (")
			.Append(center.x.ToString("F1")).Append(", ").Append(center.y.ToString("F1")).Append(", ").Append(center.z.ToString("F1"))
			.Append(") raio ").Append(radius.ToString("F1")).Append(": ").Append(valid).Append(" valido(s)").AppendLine();
		foreach (object entry in points)
		{
			Dictionary<string, object> p = (Dictionary<string, object>)entry;
			Dictionary<string, object> g = (Dictionary<string, object>)p["ground"];
			sb.Append("[BuildProbe]   r").Append(p["ring"]).Append(" d").Append(p["dir"])
				.Append(" (").Append(((float)p["x"]).ToString("F1")).Append(", ").Append(((float)p["y"]).ToString("F1")).Append(", ").Append(((float)p["z"]).ToString("F1")).Append(") ")
				.Append(p["ok"] is bool ok && ok ? "OK" : p["status"])
				.Append(" | chao=").Append(g["name"] ?? "(nada)")
				.Append(" heightmap=").Append(Yes(g["isHeightmap"]))
				.Append(" normalY=").Append(((float)g["normalY"]).ToString("F2"))
				.Append(" queda=").Append(((float)g["drop"]).ToString("F2"));
			List<string> reasons = p["reasons"] as List<string>;
			if (reasons != null && reasons.Count > 1)
			{
				sb.Append(" [").Append(string.Join(", ", reasons.ToArray())).Append(']');
			}
			sb.AppendLine();
		}
		return sb.ToString().TrimEnd();
	}

	private static string Yes(object value)
	{
		return value is bool b ? (b ? "sim" : "nao") : Convert.ToString(value);
	}

	private static string ListText(object value)
	{
		List<object> list = value as List<object>;
		if (list == null || list.Count == 0)
		{
			return "(nenhuma)";
		}
		return string.Join(", ", list.ConvertAll(o => Convert.ToString(o)).ToArray());
	}
}
