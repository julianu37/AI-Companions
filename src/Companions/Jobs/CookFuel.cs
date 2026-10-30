using System.Collections.Generic;
using UnityEngine;

namespace Companions;

// 1.4.131: the cook keeps its heat going (user: "the cooking job should also be able to refill fires and ovens. It does
// not need to fill to 100% just enough to cook with"). The fires under the cooking spots it uses (spit, iron cooking
// station, cauldron) and the oven are kept at [Cooking] FuelLevel of full: when one drops below half of that, it's
// topped back up, one piece at a time like a player. Fuel from the pack or the chests; with none, a pile of it is
// taken down (MHR_PileTaker).
public partial class CookController
{
	private MHR_PileTaker _pileTaker;

	private readonly Dictionary<int, float> _pileSkip = new Dictionary<int, float>();

	private static float FuelLevel => ModConfig.CookFuelLevel?.Value ?? 0.5f;

	// how many pieces of fuel "FuelLevel full" is (at least one)
	private static int FuelTarget(float max)
	{
		return Mathf.Max(1, Mathf.CeilToInt(max * FuelLevel));
	}

	private Task StokeTask()
	{
		Task best = null;
		float bestDist = float.MaxValue;
		Vector3 me = base.transform.position;
		foreach (KeyValuePair<Component, string> heat in HeatSources())
		{
			Component c = heat.Key;
			float max = c is Fireplace f ? f.m_maxFuel : ((CookingStation)c).m_maxFuel;
			float fuel = c.GetComponent<ZNetView>()?.GetZDO()?.GetFloat(ZDOVars.s_fuel) ?? 0f;
			int target = FuelTarget(max);
			if (fuel >= target * 0.5f && fuel >= 1f)
			{
				continue;
			}
			string item = heat.Value;
			if (Skipped(TaskKind.Stoke, item))
			{
				continue;
			}
			int n = target - Mathf.FloorToInt(fuel);
			if (n <= 0)
			{
				continue;
			}
			float d = Vector3.Distance(me, c.transform.position);
			if (d >= bestDist)
			{
				continue;
			}
			int have = Count(item);
			Task task;
			if (have > 0)
			{
				task = new Task
				{
					Kind = TaskKind.Stoke,
					Fire = c as Fireplace,
					Cooker = c as CookingStation,
					Item = item,
					Count = Mathf.Min(n, have)
				};
				AddFetch(task, item, task.Count);
			}
			else
			{
				if (ModConfig.CookUsePiles != null && !ModConfig.CookUsePiles.Value)
				{
					continue;
				}
				Piece pile = MHR_PileTaker.Find(me, Range, item, me, _pileSkip);
				if (pile == null || MHR_PileTaker.Hammer(_humanoid) == null)
				{
					continue;
				}
				task = new Task
				{
					Kind = TaskKind.Pile,
					Pile = pile,
					Item = item
				};
			}
			best = task;
			bestDist = d;
		}
		return best;
	}

	// the oven (its own fuel), and the fires under the spits, iron cooking stations and cauldrons it cooks on
	private List<KeyValuePair<Component, string>> HeatSources()
	{
		List<KeyValuePair<Component, string>> list = new List<KeyValuePair<Component, string>>();
		HashSet<int> seen = new HashSet<int>();
		List<Vector3> needFire = new List<Vector3>();
		foreach (CookingStation cooker in _cookers)
		{
			if (cooker == null)
			{
				continue;
			}
			if (cooker.m_useFuel && cooker.m_fuelItem != null && cooker.m_maxFuel > 0)
			{
				if (seen.Add(cooker.GetInstanceID()))
				{
					list.Add(new KeyValuePair<Component, string>(cooker, cooker.m_fuelItem.gameObject.name));
				}
			}
			else if (cooker.m_requireFire)
			{
				needFire.Add(cooker.transform.position);
			}
		}
		foreach (CraftingStation station in _stations)
		{
			if (station != null && station.m_craftRequireFire)
			{
				needFire.Add(station.transform.position);
			}
		}
		List<Piece> pieces = new List<Piece>();
		foreach (Vector3 at in needFire)
		{
			pieces.Clear();
			Piece.GetAllPiecesInRadius(at, 2.5f, pieces);
			foreach (Piece p in pieces)
			{
				Fireplace fire = p != null ? p.GetComponent<Fireplace>() : null;
				if (fire == null || !fire.m_canRefill || fire.m_infiniteFuel || fire.m_fuelItem == null || fire.m_maxFuel <= 0f || !seen.Add(fire.GetInstanceID()))
				{
					continue;
				}
				list.Add(new KeyValuePair<Component, string>(fire, fire.m_fuelItem.gameObject.name));
			}
		}
		return list;
	}

	// at the fire / oven: one piece at a time, as a player adds fuel
	private void Stoke()
	{
		Component c = (_task.Fire != null) ? (Component)_task.Fire : _task.Cooker;
		ZNetView nv = c != null ? c.GetComponent<ZNetView>() : null;
		Inventory bag = Bag;
		if (nv == null || !nv.IsValid() || bag == null)
		{
			return;
		}
		if (!nv.HasOwner())
		{
			nv.ClaimOwnership();
		}
		float max = _task.Fire != null ? _task.Fire.m_maxFuel : _task.Cooker.m_maxFuel;
		int target = FuelTarget(max);
		int added = 0;
		while (nv.GetZDO().GetFloat(ZDOVars.s_fuel) + added < target && MHR_Items.ConsumeOneFromInventory(bag, _task.Item))
		{
			nv.InvokeRPC("RPC_AddFuel");
			added++;
		}
		_zanim?.SetTrigger("interact");
		if (CompanionsPlugin.LogNormal) Log($"Added {added}x {Name(_task.Item)} to the {Loc(TaskName())} (kept at {FuelLevel * 100f:F0}%)");
	}

	private void UpdateTakingPile()
	{
		if (_pileTaker == null)
		{
			Finish();
			return;
		}
		int r = _pileTaker.Tick(_ai, _humanoid, _setup);
		if (r == 0)
		{
			return;
		}
		if (r < 0)
		{
			if (_pileTaker.Pile != null)
			{
				_pileSkip[_pileTaker.Pile.GetInstanceID()] = Time.time + 600f;
			}
			if (CompanionsPlugin.LogNormal) Log("Gave up on the pile: " + _pileTaker.Why);
		}
		else
		{
			if (CompanionsPlugin.LogNormal) Log("Took down a pile - picked up " + _pileTaker.Got + " " + MHR_List.Local(_pileTaker.Shared));
		}
		_ai?.StopMoving();
		MHR_Mover.Release(_ai);
		Finish();
		_scanTimer = 0.5f;
	}

	private void DropPileTaker()
	{
		if (_pileTaker != null)
		{
			_pileTaker.PutAway(_humanoid, _setup);
			_pileTaker = null;
		}
	}
}
