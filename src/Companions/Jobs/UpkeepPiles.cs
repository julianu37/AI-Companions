using System;
using System.Collections.Generic;
using Splatform;
using UnityEngine;

namespace Companions;

// 1.4.126: Repair and Restock also stacks wood and stone into piles (user: "should also make wood and stone piles as
// they are sorting the chests"). Agreed: only next to piles of the same kind the player already built (the game's Stack
// of Wood, Stone Pile, Fine Wood Stack... - any player-built piece named *stack* / *pile* made from one material), and
// only what's above [RepairAndRestock] PileKeep in the chests. Built like a player: hammer in hand, the material taken
// from the chests and used up, the piece's crafting station in range, ward access, stamina and hammer wear.
public partial class UpkeepController
{
	private sealed class PileKind
	{
		public string Item;          // material prefab, e.g. "Wood"

		public string Shared;        // its in-game token, e.g. "$item_wood"

		public int Cost;

		public GameObject Prefab;

		public readonly List<Piece> Piles = new List<Piece>();
	}

	private PileKind _pile;

	private bool _pileHasSpot;

	private Vector3 _pileSpot;

	private Quaternion _pileRot;

	private Piece _pileNextTo;

	private Vector3 _pileStand;

	private readonly Dictionary<string, float> _pileNoSpot = new Dictionary<string, float>();

	private readonly HashSet<Vector3Int> _pileBadSpots = new HashSet<Vector3Int>();

	private static int s_buildMask = -1;

	private bool PlanPiles()
	{
		if (ModConfig.UpkeepPiles != null && !ModConfig.UpkeepPiles.Value)
		{
			return false;
		}
		if (Hammer() == null)
		{
			return false;
		}
		MHR_List allow = MHR_List.Get(ModConfig.UpkeepPileItems?.Value ?? "*");
		int keepDefault = ModConfig.UpkeepPileKeep?.Value ?? 100;
		List<Container> chests = Chests();
		foreach (PileKind k in PileKinds())
		{
			float until;
			if (_pileNoSpot.TryGetValue(k.Item, out until) && Time.time < until)
			{
				continue;
			}
			if (!allow.Allows(k.Item, MHR_List.Local(k.Shared)))
			{
				continue;
			}
			int keep = allow.AmountFor(k.Item, keepDefault);
			int total = 0;
			Container from = null;
			int most = 0;
			foreach (Container c in chests)
			{
				int n = c.GetInventory().CountItems(k.Shared);
				total += n;
				if (n > most)
				{
					most = n;
					from = c;
				}
			}
			// 1.4.129: the other way round (user: piles are a resource too) - chests below the amount kept: take a pile down
			// and put the material back in the chests. Not while it's still carrying some, and only with room for it.
			int carried;
			_carried.TryGetValue(k.Shared, out carried);
			if (total + carried < keep && carried == 0 && PlanTakeDown(k, total, keep, chests))
			{
				return true;
			}
			int piles = (total - keep) / Mathf.Max(1, k.Cost);
			if (piles <= 0 || from == null)
			{
				continue;
			}
			// as many as it can carry in one go, from the chest holding the most
			float w = WeightOf(from, k.Shared);
			int carry = w > 0f ? Mathf.FloorToInt((CompanionCarry.Max(_humanoid) - _humanoid.GetInventory().GetTotalWeight()) / (w * k.Cost)) : piles;
			int take = Mathf.Min(piles, Mathf.Min(carry, most / k.Cost)) * k.Cost;
			if (take < k.Cost)
			{
				continue;
			}
			_pile = k;
			_chest = from;
			_key = k.Shared;
			_amount = take;
			_pileHasSpot = false;
			if (CompanionsPlugin.LogNormal) Log("Stacking " + take + " " + MHR_List.Local(k.Shared) + " into " + (take / k.Cost) + " " + MHR_List.Local(k.Piles[0].m_name) + "(s) next to the existing ones (" + total + " in the chests, keeping " + keep + ")");
			Enter(Step.PileTake);
			return true;
		}
		return false;
	}

	private static float WeightOf(Container c, string shared)
	{
		foreach (ItemDrop.ItemData it in c.GetInventory().GetAllItems())
		{
			if (it.m_shared.m_name == shared)
			{
				return it.m_shared.m_weight;
			}
		}
		return 0f;
	}

	// the kinds of pile the player has built in the work area
	private List<PileKind> PileKinds()
	{
		Dictionary<string, PileKind> kinds = new Dictionary<string, PileKind>();
		s_pieces.Clear();
		Piece.GetAllPiecesInRadius(Center(), Range(), s_pieces);
		foreach (Piece p in s_pieces)
		{
			if (!IsPile(p))
			{
				continue;
			}
			Piece.Requirement req = p.m_resources[0];
			string item = req.m_resItem.gameObject.name;
			PileKind k;
			if (!kinds.TryGetValue(item, out k))
			{
				GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(Utils.GetPrefabName(p.gameObject)) : null;
				if (prefab == null)
				{
					continue;
				}
				k = new PileKind
				{
					Item = item,
					Shared = req.m_resItem.m_itemData.m_shared.m_name,
					Cost = Mathf.Max(1, req.m_amount),
					Prefab = prefab
				};
				kinds[item] = k;
			}
			k.Piles.Add(p);
		}
		return new List<PileKind>(kinds.Values);
	}

	internal static bool IsPile(Piece p)
	{
		if (p == null || !p.IsPlacedByPlayer() || p.m_resources == null || p.m_resources.Length != 1 || p.m_resources[0].m_resItem == null)
		{
			return false;
		}
		string n = Utils.GetPrefabName(p.gameObject).ToLowerInvariant();
		if (!n.Contains("stack") && !n.Contains("pile"))
		{
			return false;
		}
		return p.GetComponentInChildren<Container>() == null && p.GetComponentInChildren<Fireplace>() == null && p.GetComponentInChildren<CraftingStation>() == null && p.GetComponentInChildren<Bed>() == null && p.GetComponentInChildren<Door>() == null;
	}

	private void PileTaken(Container c, Inventory inv, Inventory bag)
	{
		int got = Move(inv, bag, _key, byName: true, _amount);
		Took(_key, got);
		CloseChest(c);
		PileKind k = _pile;
		if (k == null || got < k.Cost)
		{
			Done("Not enough " + MHR_List.Local(_key) + " for a pile after all (" + got + ")");
			return;
		}
		if (CompanionsPlugin.LogDetailed) LogDebug("Took " + got + " " + MHR_List.Local(_key) + " for piles");
		Done(null);
		_pile = k;
		_pileHasSpot = false;
		Enter(Step.PileBuild);
	}

	private void UpdatePileBuild()
	{
		PileKind k = _pile;
		Inventory bag = _humanoid.GetInventory();
		if (k == null || bag.CountItems(k.Shared) < k.Cost)
		{
			_pile = null;
			Done(null);   // leftovers go back with the next put-away
			return;
		}
		ItemDrop.ItemData hammer = Hammer();
		if (hammer == null)
		{
			_pile = null;
			Done("No working hammer - no piles for now");
			return;
		}
		if (!_pileHasSpot)
		{
			if (!PileSpot(k))
			{
				_pileNoSpot[k.Item] = Time.time + 600f;
				if (CompanionsPlugin.LogNormal) Log("No free spot next to the existing " + MHR_List.Local(k.Shared) + " piles - trying again in 10 minutes");
				_pile = null;
				Done(null);
				return;
			}
			_pileHasSpot = true;
			_stepStart = Time.time;
			_arrived = false;
			_arrivedAt = 0f;
			Vector3 stand;
			_pileStand = MHR_Chests.PickApproachPoint(base.transform.position, _pileSpot, _pileNextTo, out stand) ? stand : _pileSpot;
		}
		Vector3 me = base.transform.position;
		Vector3 aim = _pileSpot + Vector3.up * 0.4f;
		if (!PointInReach(me, aim, _pileNextTo) || Vector3.Distance(me, _pileSpot) > Reach)
		{
			bool far = Time.time - _stepStart > GiveUp;
			_arrived = MHR_Nav.Move(_ai, Time.deltaTime, _pileStand, _pileStand == _pileSpot ? 1.5f : 0.6f, run: true, Tag);
			if (_arrived && _arrivedAt == 0f)
			{
				_arrivedAt = Time.time;
			}
			if (far || MHR_Mover.GaveUp(_ai) || (_arrived && Time.time - _arrivedAt > 3f))
			{
				if (CompanionsPlugin.LogDetailed) LogDebug("Can't get to the pile spot " + MHR_Mover.Fmt(_pileSpot) + " - trying another");
				_pileBadSpots.Add(Cell(_pileSpot));
				_pileHasSpot = false;
				MHR_Mover.Release(_ai);
			}
			return;
		}
		_ai.StopMoving();
		MHR_Mover.Release(_ai);
		if (!_humanoid.IsItemEquiped(hammer))
		{
			_setup.SuppressAutoEquip = true;
			_holdingHammer = true;
			_humanoid.EquipItem(hammer);
			_nextAct = Time.time + 0.6f;
			return;
		}
		_ai.LookAtPoint(aim);
		if (Time.time < _nextAct || _character.InAttack() || !_ai.IsLookingAtPoint(aim, 15f))
		{
			return;
		}
		float cost = SwingStamina(hammer);
		if (_stamina != null && _stamina.Stamina < cost)
		{
			return;
		}
		// still free? (someone may have put something there meanwhile)
		Piece prefabPiece = k.Prefab.GetComponent<Piece>();
		if (!SpotFree(k, _pileSpot, _pileRot))
		{
			_pileBadSpots.Add(Cell(_pileSpot));
			_pileHasSpot = false;
			return;
		}
		bool noBench = ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoWorkbench);
		if (!noBench && prefabPiece != null && prefabPiece.m_craftingStation != null && CraftingStation.HaveBuildStationInRange(prefabPiece.m_craftingStation.m_name, _pileSpot) == null)
		{
			_pileBadSpots.Add(Cell(_pileSpot));
			_pileHasSpot = false;
			if (CompanionsPlugin.LogDetailed) LogDebug("No " + MHR_List.Local(prefabPiece.m_craftingStation.m_name) + " in range of " + MHR_Mover.Fmt(_pileSpot));
			return;
		}
		if (!PrivateArea.CheckAccess(_pileSpot, 0f, flash: false))
		{
			_pileBadSpots.Add(Cell(_pileSpot));
			_pileHasSpot = false;
			return;
		}
		_nextAct = Time.time + WorkTime;
		PlacePile(k, hammer, cost);
		_pileHasSpot = false;
	}

	// what Player.PlacePiece does, then the material, stamina and hammer wear
	private void PlacePile(PileKind k, ItemDrop.ItemData hammer, float stamina)
	{
		Inventory bag = _humanoid.GetInventory();
		TerrainModifier.SetTriggerOnPlaced(trigger: true);
		GameObject go = UnityEngine.Object.Instantiate(k.Prefab, _pileSpot, _pileRot);
		TerrainModifier.SetTriggerOnPlaced(trigger: false);
		Piece piece = go.GetComponent<Piece>();
		if (piece != null)
		{
			PlatformUserID user = default(PlatformUserID);
			try
			{
				user = PlatformManager.DistributionPlatform.LocalUser.PlatformUserID;
			}
			catch
			{
			}
			piece.SetCreator(MHR_Owner.CreatorId(this), user);   // 1.4.152: built for its own player
		}
		go.GetComponent<WearNTear>()?.OnPlaced();
		string anim = hammer.m_shared.m_attack?.m_attackAnimation;
		GetComponent<ZSyncAnimation>()?.SetTrigger(!string.IsNullOrEmpty(anim) ? anim : "swing_hammer");
		if (piece != null)
		{
			piece.m_placeEffect.Create(_pileSpot, _pileRot, go.transform, 1f, -1, _character.GetZDOID());
		}
		bag.RemoveItem(k.Shared, k.Cost);
		Took(k.Shared, -k.Cost);
		_stamina?.UseStamina(stamina);
		if (hammer.m_shared.m_useDurability)
		{
			hammer.m_durability = Mathf.Max(0f, hammer.m_durability - hammer.m_shared.m_useDurabilityDrain * Game.m_durabilityRate);
		}
		if (piece != null)
		{
			k.Piles.Add(piece);
		}
		if (CompanionsPlugin.LogNormal) Log("Built a " + (piece != null ? MHR_List.Local(piece.m_name) : k.Prefab.name) + " at " + MHR_Mover.Fmt(_pileSpot) + " from " + k.Cost + " " + MHR_List.Local(k.Shared));
	}

	// ---- where: right beside (or on top of) one of the same kind ----

	// 1.4.129: every free spot beside (or on top of) a pile of this kind gets a score and the best wins (user: it kept
	// growing the row into the main path; next to a wall or the back of the furnace is fine, by its ports isn't).
	//  - rejected: narrows a walkway (something 0.1-1.6 m away on a side - too tight to walk past), or within reach of
	//    a door, portal, bed, chair, smelter/kiln port, crafting station, cooking spot, fermenter, beehive or chest
	//  - better: sides backed by a wall, another pile or something solid (never on top of a pile - 1.4.132)
	//  - a little worse the further it is from the piles it's next to
	private bool PileSpot(PileKind k)
	{
		Vector3 me = base.transform.position;
		List<Piece> piles = new List<Piece>(k.Piles);
		piles.RemoveAll(p => p == null);
		piles.Sort((a, b) => Vector3.Distance(me, a.transform.position).CompareTo(Vector3.Distance(me, b.transform.position)));
		if (piles.Count > 24)
		{
			piles.RemoveRange(24, piles.Count - 24);
		}
		float bestScore = float.MinValue;
		bool found = false;
		int rejectedPath = 0, rejectedUse = 0;
		foreach (Piece pile in piles)
		{
			Vector3 center, half;
			if (!LocalBox(pile, out center, out half))
			{
				continue;
			}
			Vector3[] offsets =
			{
				new Vector3(half.x * 2f + 0.02f, 0f, 0f),
				new Vector3(-half.x * 2f - 0.02f, 0f, 0f),
				new Vector3(0f, 0f, half.z * 2f + 0.02f),
				new Vector3(0f, 0f, -half.z * 2f - 0.02f)
			};
			for (int i = 0; i < offsets.Length; i++)
			{
				Vector3 pos = pile.transform.TransformPoint(offsets[i]);
				Quaternion rot = pile.transform.rotation;
				if (_pileBadSpots.Contains(Cell(pos)) || !InArea(pos))
				{
					continue;
				}
				// 1.4.132: never on top of another pile - 1.4.131 test: a Stone Pile put on top of one broke (piles can't
				// carry each other); only beside, on the ground or floor
				if (!SpotFree(k, pos, rot) || !Supported(pos, rot, center, half))
				{
					continue;
				}
				if (NearUsedThing(pos + rot * center, half))
				{
					rejectedUse++;
					continue;
				}
				int backed;
				if (!SidesOk(pile, pos, rot, center, half, out backed))
				{
					rejectedPath++;
					continue;
				}
				float score = backed * 2f;
				score -= Vector3.Distance(pos, piles[0].transform.position) * 0.05f;
				if (score > bestScore)
				{
					bestScore = score;
					_pileSpot = pos;
					_pileRot = rot;
					_pileNextTo = pile;
					found = true;
				}
			}
		}
		if (!found && rejectedPath + rejectedUse > 0)
		{
			if (CompanionsPlugin.LogNormal) Log("Every spot beside the " + MHR_List.Local(k.Shared) + " piles would be in the way (" + rejectedPath + " in a walkway, " + rejectedUse + " by a door, station, port or chest)");
		}
		else if (found)
		{
			if (CompanionsPlugin.LogDetailed) LogDebug("Pile spot " + MHR_Mover.Fmt(_pileSpot) + " (score " + bestScore.ToString("F1") + ")");
		}
		return found;
	}

	private static float GroundBelow(Vector3 p)
	{
		RaycastHit hit;
		if (Physics.Raycast(p + Vector3.up * 0.2f, Vector3.down, out hit, 10f, LayerMask.GetMask("Default", "static_solid", "piece", "terrain")))
		{
			return hit.point.y;
		}
		return p.y;
	}

	// each open side: something within 0.1 m = backed (good), 0.1-1.6 m = a gap too narrow to walk through (in the way),
	// further = open. Checked at knee and chest height.
	private static bool SidesOk(Piece neighbour, Vector3 pos, Quaternion rot, Vector3 center, Vector3 half, out int backed)
	{
		backed = 0;
		Vector3 mid = pos + rot * center;
		Vector3[] dirs = { rot * Vector3.right, rot * Vector3.left, rot * Vector3.forward, rot * Vector3.back };
		float[] reach = { half.x, half.x, half.z, half.z };
		int mask = LayerMask.GetMask("Default", "static_solid", "piece", "terrain", "vehicle");
		for (int d = 0; d < 4; d++)
		{
			float gap = float.MaxValue;
			foreach (float hgt in new[] { 0.4f, 1.2f })
			{
				Vector3 from = new Vector3(mid.x, pos.y + hgt, mid.z) + dirs[d] * (reach[d] + 0.01f);
				RaycastHit hit;
				if (Physics.Raycast(from, dirs[d], out hit, 1.6f, mask) && !hit.collider.isTrigger)
				{
					Piece hp = hit.collider.GetComponentInParent<Piece>();
					if (hp != null && hp == neighbour)
					{
						gap = 0f;
						continue;
					}
					gap = Mathf.Min(gap, hit.distance);
				}
			}
			if (gap <= 0.1f)
			{
				backed++;
			}
			else if (gap < 1.6f)
			{
				return false;
			}
		}
		return true;
	}

	// too close to something people walk up to and use
	private static bool NearUsedThing(Vector3 mid, Vector3 half)
	{
		float r = Mathf.Max(half.x, half.z) + 1.6f;
		foreach (Collider c in Physics.OverlapSphere(mid, r + 1f, LayerMask.GetMask("Default", "piece", "piece_nonsolid", "static_solid", "vehicle")))
		{
			if (c == null)
			{
				continue;
			}
			Vector3 at;
			float need;
			if (UsePoint(c, out at, out need) && Vector3.Distance(new Vector3(at.x, mid.y, at.z), mid) < Mathf.Max(half.x, half.z) + need)
			{
				return true;
			}
		}
		return false;
	}

	// where a thing is used from and how much room that needs
	private static bool UsePoint(Collider c, out Vector3 at, out float need)
	{
		at = c.transform.position;
		need = 0f;
		Smelter sm = c.GetComponentInParent<Smelter>();
		if (sm != null)
		{
			// the sides with the ports, not the back
			float best = float.MaxValue;
			foreach (Switch sw in new[] { sm.m_addOreSwitch, sm.m_addWoodSwitch, sm.m_emptyOreSwitch })
			{
				if (sw == null)
				{
					continue;
				}
				float d = Vector3.Distance(c.ClosestPoint(sw.transform.position), sw.transform.position);
				if (d < best)
				{
					best = d;
					at = sw.transform.position;
				}
			}
			need = 1.6f;
			return best < float.MaxValue;
		}
		if (c.GetComponentInParent<Door>() != null || c.GetComponentInParent<TeleportWorld>() != null)
		{
			at = c.GetComponentInParent<Piece>() != null ? c.GetComponentInParent<Piece>().transform.position : at;
			need = 2.0f;
			return true;
		}
		Component used = (Component)c.GetComponentInParent<Bed>() ?? (Component)c.GetComponentInParent<Chair>() ?? (Component)c.GetComponentInParent<CraftingStation>() ?? (Component)c.GetComponentInParent<CookingStation>() ?? (Component)c.GetComponentInParent<Fermenter>() ?? (Component)c.GetComponentInParent<Beehive>() ?? (Component)c.GetComponentInParent<Fireplace>();
		if (used != null)
		{
			at = used.transform.position;
			need = 1.4f;
			return true;
		}
		Container box = c.GetComponentInParent<Container>();
		if (box != null && box.GetComponentInParent<Character>() == null)
		{
			at = box.transform.position;
			need = 1.0f;
			return true;
		}
		return false;
	}

	private bool InArea(Vector3 p)
	{
		return Vector3.Distance(Center(), p) <= Range();
	}

	private static Vector3Int Cell(Vector3 p)
	{
		return new Vector3Int(Mathf.RoundToInt(p.x * 2f), Mathf.RoundToInt(p.y * 2f), Mathf.RoundToInt(p.z * 2f));
	}

	// the pile's size in its own space, from its colliders
	private static bool LocalBox(Piece pile, out Vector3 center, out Vector3 half)
	{
		center = Vector3.zero;
		half = Vector3.zero;
		Transform t = pile.transform;
		bool any = false;
		Bounds lb = default(Bounds);
		foreach (Collider col in pile.GetComponentsInChildren<Collider>())
		{
			if (col == null || !col.enabled || col.isTrigger)
			{
				continue;
			}
			Bounds cb;
			if (col is BoxCollider bc)
			{
				cb = new Bounds(bc.center, bc.size);
			}
			else if (col is MeshCollider mc && mc.sharedMesh != null)
			{
				cb = mc.sharedMesh.bounds;
			}
			else
			{
				continue;
			}
			for (int i = 0; i < 8; i++)
			{
				Vector3 corner = cb.center + Vector3.Scale(cb.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
				Vector3 local = t.InverseTransformPoint(col.transform.TransformPoint(corner));
				if (!any)
				{
					lb = new Bounds(local, Vector3.zero);
					any = true;
				}
				else
				{
					lb.Encapsulate(local);
				}
			}
		}
		if (!any || lb.size.x < 0.2f || lb.size.z < 0.2f)
		{
			return false;
		}
		center = lb.center;
		half = lb.extents;
		return true;
	}

	private static int BuildMask()
	{
		if (s_buildMask == -1)
		{
			s_buildMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "vehicle", "character", "character_net", "character_noenv");
		}
		return s_buildMask;
	}

	// nothing in the way where the new pile would go (the ground itself doesn't count)
	private static bool SpotFree(PileKind k, Vector3 pos, Quaternion rot)
	{
		Piece prefabPiece = k.Prefab.GetComponent<Piece>();
		Vector3 center, half;
		if (prefabPiece == null || !LocalBoxOf(k, out center, out half))
		{
			return false;
		}
		// a little smaller than the pile and lifted off its base, so the floor or ground it stands on doesn't count
		Vector3 box = new Vector3(half.x * 0.9f, half.y * 0.8f, half.z * 0.9f);
		Collider[] hits = Physics.OverlapBox(pos + rot * (center + Vector3.up * half.y * 0.15f), box, rot, BuildMask());
		foreach (Collider h in hits)
		{
			if (h != null && !h.isTrigger)
			{
				return false;
			}
		}
		return true;
	}

	private static readonly Dictionary<string, KeyValuePair<Vector3, Vector3>> s_prefabBox = new Dictionary<string, KeyValuePair<Vector3, Vector3>>();

	private static bool LocalBoxOf(PileKind k, out Vector3 center, out Vector3 half)
	{
		KeyValuePair<Vector3, Vector3> box;
		if (s_prefabBox.TryGetValue(k.Item, out box))
		{
			center = box.Key;
			half = box.Value;
			return true;
		}
		center = Vector3.zero;
		half = Vector3.zero;
		foreach (Piece p in k.Piles)
		{
			if (p != null && LocalBox(p, out center, out half))
			{
				s_prefabBox[k.Item] = new KeyValuePair<Vector3, Vector3>(center, half);
				return true;
			}
		}
		return false;
	}

	// something to stand on right under where the bottom of the new pile would be
	private static bool Supported(Vector3 pos, Quaternion rot, Vector3 center, Vector3 half)
	{
		Vector3 bottom = pos + rot * new Vector3(center.x, center.y - half.y, center.z);
		RaycastHit hit;
		if (!Physics.Raycast(bottom + Vector3.up * 0.5f, Vector3.down, out hit, 1.0f, LayerMask.GetMask("Default", "static_solid", "piece", "terrain")))
		{
			return false;
		}
		if (IsPile(hit.collider.GetComponentInParent<Piece>()))
		{
			return false;   // 1.4.132: resting on another pile breaks it
		}
		return Mathf.Abs(hit.point.y - bottom.y) <= 0.3f;
	}
}

// 1.4.129: taking a pile down to refill the chests - the hammer's remove, like a player: the piece breaks, the material
// drops (or lands in the little loot box some piles leave), it's picked up and then put back in the chests.
public partial class UpkeepController
{
	private Piece _downPile;

	private float _downAt;

	private bool PlanTakeDown(PileKind k, int total, int keep, List<Container> chests)
	{
		bool room = false;
		foreach (Container c in chests)
		{
			if (RoomInByName(c, k.Shared))
			{
				room = true;
				break;
			}
		}
		if (!room)
		{
			return false;
		}
		// the top of a stack first, then the nearest
		Vector3 me = base.transform.position;
		Piece best = null;
		float bestScore = float.MaxValue;
		foreach (Piece p in k.Piles)
		{
			if (p == null || Skipped(p) || !p.m_canBeRemoved || !PrivateArea.CheckAccess(p.transform.position, 0f, flash: false))
			{
				continue;
			}
			float score = Vector3.Distance(me, p.transform.position) - (p.transform.position.y - GroundBelow(p.transform.position + Vector3.down * 0.05f)) * 20f;
			if (score < bestScore)
			{
				bestScore = score;
				best = p;
			}
		}
		if (best == null)
		{
			return false;
		}
		_pile = k;
		_downPile = best;
		if (CompanionsPlugin.LogNormal) Log("Chests are down to " + total + " " + MHR_List.Local(k.Shared) + " (keeping " + keep + ") - taking down the " + MHR_List.Local(best.m_name) + " at " + MHR_Mover.Fmt(best.transform.position));
		Enter(Step.PileDown);
		return true;
	}

	private static bool RoomInByName(Container c, string shared)
	{
		Inventory inv = c.GetInventory();
		if (inv.GetEmptySlots() > 0)
		{
			return true;
		}
		foreach (ItemDrop.ItemData it in inv.GetAllItems())
		{
			if (it.m_shared.m_name == shared && it.m_stack < it.m_shared.m_maxStackSize)
			{
				return true;
			}
		}
		return false;
	}

	private void UpdatePileDown()
	{
		PileKind k = _pile;
		Piece pile = _downPile;
		if (k == null || pile == null || pile.GetComponent<ZNetView>() == null || !pile.GetComponent<ZNetView>().IsValid())
		{
			_pile = null;
			Done("The pile is gone");
			return;
		}
		ItemDrop.ItemData hammer = Hammer();
		if (hammer == null)
		{
			_pile = null;
			Done("No working hammer - can't take a pile down");
			return;
		}
		Vector3 me = base.transform.position;
		WearNTear w = pile.GetComponent<WearNTear>();
		Vector3 aim = pile.transform.position + Vector3.up * 0.4f;
		bool inReach = w != null ? PieceInReach(me, w, out aim) : PointInReach(me, aim, pile);
		if (!inReach)
		{
			if (Time.time - _stepStart > GiveUp)
			{
				_pile = null;
				Skip(pile, 600f, "Couldn't get within reach of the pile to take it down");
				return;
			}
			if (!Approach(pile.transform.position, pile))
			{
				_pile = null;
				Skip(pile, 600f, "No way to get to the pile");
				return;
			}
			if (!_arrived)
			{
				_arrivedAt = 0f;
			}
			else if (_arrivedAt == 0f)
			{
				_arrivedAt = Time.time;
			}
			else if (Time.time - _arrivedAt > 3f)
			{
				_pile = null;
				Skip(pile, 600f, "Can't reach the pile from where it can stand");
			}
			return;
		}
		_ai.StopMoving();
		MHR_Mover.Release(_ai);
		if (!_humanoid.IsItemEquiped(hammer))
		{
			_setup.SuppressAutoEquip = true;
			_holdingHammer = true;
			_humanoid.EquipItem(hammer);
			_nextAct = Time.time + 0.6f;
			return;
		}
		_ai.LookAtPoint(aim);
		if (Time.time < _nextAct || _character.InAttack() || !_ai.IsLookingAtPoint(aim, 15f))
		{
			return;
		}
		// the game's checks for removing a piece: its crafting station in range, ward access, allowed right now
		bool noBench = ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoWorkbench);
		if (!noBench && pile.m_craftingStation != null && CraftingStation.HaveBuildStationInRange(pile.m_craftingStation.m_name, me) == null)
		{
			_pile = null;
			Skip(pile, 600f, "No " + MHR_List.Local(pile.m_craftingStation.m_name) + " in range to take the pile down");
			return;
		}
		if (!pile.CanBeRemoved())
		{
			_pile = null;
			Skip(pile, 120f, "The pile can't be taken down right now");
			return;
		}
		Vector3 at = pile.transform.position;
		_pileDropsBefore = MHR_Chests.DropsNear(at, 4f);   // 1.4.152: only what this pile drops is picked up
		ZNetView nv = pile.GetComponent<ZNetView>();
		if (!nv.IsOwner())
		{
			nv.ClaimOwnership();
		}
		pile.GetComponent<IRemoved>()?.OnRemoved();
		if (w != null)
		{
			w.Remove();
		}
		else
		{
			pile.DropResources();
			pile.m_placeEffect.Create(at, pile.transform.rotation, pile.transform, 1f, -1, _character.GetZDOID());
			ZNetScene.instance.Destroy(pile.gameObject);
		}
		string anim = hammer.m_shared.m_attack?.m_attackAnimation;
		GetComponent<ZSyncAnimation>()?.SetTrigger(!string.IsNullOrEmpty(anim) ? anim : "swing_hammer");   // removing costs no stamina or wear, as for a player
		k.Piles.Remove(pile);
		if (CompanionsPlugin.LogNormal) Log("Took down the " + MHR_List.Local(pile.m_name) + " at " + MHR_Mover.Fmt(at));
		_downAt = Time.time;
		_spot = at;
		_downPile = null;
		Enter(Step.PileCollect);
		_spot = at;
	}

	private HashSet<int> _pileDropsBefore = new HashSet<int>();

	// pick up what fell out (and empty a loot box if the pile left one)
	private void UpdatePileCollect()
	{
		PileKind k = _pile;
		if (k == null || Time.time - _stepStart > 30f)
		{
			_pile = null;
			Done(null);   // whatever was picked up goes back in the chests with the next put-away
			return;
		}
		if (Time.time - _downAt < 0.6f)
		{
			return;
		}
		Inventory bag = _humanoid.GetInventory();
		Vector3 me = base.transform.position;
		ItemDrop nearest = null;
		float nd = float.MaxValue;
		foreach (Collider c in Physics.OverlapSphere(_spot, 4f))
		{
			ItemDrop d = c != null ? c.GetComponentInParent<ItemDrop>() : null;
			if (d != null && d.m_itemData != null && d.m_itemData.m_shared.m_name == k.Shared && !_pileDropsBefore.Contains(d.GetInstanceID()) && bag.CanAddItem(d.m_itemData, 1))
			{
				float dist = Vector3.Distance(me, d.transform.position);
				if (dist < nd)
				{
					nd = dist;
					nearest = d;
				}
				continue;
			}
			Container box = c != null ? c.GetComponentInParent<Container>() : null;
			if (box != null && box.GetComponent<Piece>() == null && box.GetComponentInParent<Character>() == null && box.GetInventory() != null && box.GetInventory().CountItems(k.Shared) > 0 && Vector3.Distance(me, box.transform.position) < 2.5f)
			{
				ZNetView bnv = box.GetComponent<ZNetView>();
				if (bnv != null && !bnv.IsOwner())
				{
					bnv.ClaimOwnership();
				}
				GetComponent<ZSyncAnimation>()?.SetTrigger("interact");
				int got = Move(box.GetInventory(), bag, k.Shared, byName: true, int.MaxValue);
				Took(k.Shared, got);
				if (CompanionsPlugin.LogDetailed) LogDebug("Took " + got + " " + MHR_List.Local(k.Shared) + " out of what the pile left");
				return;
			}
		}
		if (nearest == null)
		{
			_pile = null;
			Done(null);
			return;
		}
		if (nd > 1.5f)
		{
			MHR_Nav.Move(_ai, Time.deltaTime, nearest.transform.position, 1.0f, run: false, Tag);
			return;
		}
		if (!nearest.CanPickup())
		{
			nearest.RequestOwn();
			_ai.StopMoving();
			return;
		}
		int n = nearest.m_itemData.m_stack;
		if (_humanoid.Pickup(nearest.gameObject, autoequip: false, autoPickupDelay: false))
		{
			Took(k.Shared, n);
		}
	}
}
