using System.Collections.Generic;
using UnityEngine;

namespace Companions;

// 1.4.133: Repair and Restock also empties the beehives and puts the honey away (user). Like a player: walk up to the
// hive, use it (the game's own extract - the honey pops out), pick the honey up; the put-away step then takes it to
// the chest that already holds the most honey.
public partial class UpkeepController
{
	private Beehive _hive;

	private Vector3 _hiveAt;

	private HashSet<int> _dropsBefore = new HashSet<int>();

	private string _honey;

	private bool PlanHoney()
	{
		if (ModConfig.UpkeepHoney != null && !ModConfig.UpkeepHoney.Value)
		{
			return false;
		}
		float atLeast = ModConfig.UpkeepHoneyAt?.Value ?? 0.5f;
		Vector3 me = base.transform.position;
		Beehive best = null;
		float bestD = float.MaxValue;
		s_pieces.Clear();
		Piece.GetAllPiecesInRadius(Center(), Range(), s_pieces);
		foreach (Piece p in s_pieces)
		{
			Beehive hive = p != null ? p.GetComponent<Beehive>() : null;
			if (hive == null || hive.m_honeyItem == null || Skipped(hive))
			{
				continue;
			}
			int honey = HoneyIn(hive);
			if (honey <= 0 || honey < Mathf.CeilToInt(hive.m_maxHoney * atLeast))
			{
				continue;
			}
			if (!PrivateArea.CheckAccess(hive.transform.position, 0f, flash: false))
			{
				continue;
			}
			float d = Vector3.Distance(me, hive.transform.position);
			if (d < bestD)
			{
				bestD = d;
				best = hive;
			}
		}
		if (best == null)
		{
			return false;
		}
		_hive = best;
		_honey = best.m_honeyItem.m_itemData.m_shared.m_name;
		if (CompanionsPlugin.LogNormal) Log("Collecting " + HoneyIn(best) + " " + MHR_List.Local(_honey) + " from the beehive at " + MHR_Mover.Fmt(best.transform.position));
		Enter(Step.Hive);
		return true;
	}

	private static int HoneyIn(Beehive hive)
	{
		return hive.GetComponent<ZNetView>()?.GetZDO()?.GetInt(ZDOVars.s_level) ?? 0;
	}

	private void UpdateHive()
	{
		Beehive hive = _hive;
		if (hive == null || hive.GetComponent<ZNetView>() == null || !hive.GetComponent<ZNetView>().IsValid())
		{
			Done("The beehive is gone");
			return;
		}
		if (HoneyIn(hive) <= 0)
		{
			Done("Someone emptied the beehive first");
			return;
		}
		Vector3 me = base.transform.position;
		Vector3 point = hive.transform.position + Vector3.up * 0.5f;
		if (!PointInReach(me, point, hive))
		{
			if (Time.time - _stepStart > GiveUp)
			{
				Skip(hive, 300f, "Couldn't get within reach of the beehive - leaving it for now");
				return;
			}
			if (!Approach(hive.transform.position, hive))
			{
				Skip(hive, 300f, "No way to get to the beehive");
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
				Skip(hive, 300f, "Can't reach the beehive from where it can stand");
			}
			return;
		}
		_ai.StopMoving();
		MHR_Mover.Release(_ai);
		_ai.LookAtPoint(point);
		if (_nextAct == 0f)
		{
			_nextAct = Time.time + 0.4f;
			return;
		}
		if (Time.time < _nextAct || !_ai.IsLookingAtPoint(point, 20f))
		{
			return;
		}
		GetComponent<ZSyncAnimation>()?.SetTrigger("interact");
		_hiveAt = hive.m_spawnPoint != null ? hive.m_spawnPoint.position : hive.transform.position;
		_dropsBefore = MHR_Chests.DropsNear(_hiveAt, 3f);   // 1.4.152: only the honey from this hive is picked up
		hive.GetComponent<ZNetView>().InvokeRPC("RPC_Extract");
		Enter(Step.HiveCollect);
	}

	private void UpdateHiveCollect()
	{
		if (Time.time - _stepStart > 20f)
		{
			Done(null);
			return;
		}
		if (Time.time - _stepStart < 0.6f)
		{
			return;
		}
		Inventory bag = _humanoid.GetInventory();
		Vector3 me = base.transform.position;
		ItemDrop nearest = null;
		float nd = float.MaxValue;
		foreach (Collider c in Physics.OverlapSphere(_hiveAt, 3f))
		{
			ItemDrop d = c != null ? c.GetComponentInParent<ItemDrop>() : null;
			if (d == null || d.m_itemData == null || d.m_itemData.m_shared.m_name != _honey || _dropsBefore.Contains(d.GetInstanceID()) || !bag.CanAddItem(d.m_itemData, 1))
			{
				continue;
			}
			float dist = Vector3.Distance(me, d.transform.position);
			if (dist < nd)
			{
				nd = dist;
				nearest = d;
			}
		}
		if (nearest == null)
		{
			if (CompanionsPlugin.LogNormal) Log("Picked up the honey - putting it away");
			Done(null);   // the put-away step takes it to the chest with the most honey
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
			Took(_honey, n);
		}
	}
}
