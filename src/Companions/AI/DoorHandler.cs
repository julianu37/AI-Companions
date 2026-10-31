using UnityEngine;

namespace Companions;

// 1.4.141 (movement phase 4 part 2): only the "open this door" command is left here. MHR_Mover opens and closes doors on
// the way, lines up with doorways and handles doors left open towards it, so the old proactive door scanning, its stuck
// timers and approach/pass-through/close phases (and the [Door] settings) are gone.
public class DoorHandler : MonoBehaviour
{
	private CompanionAI _ai;

	private Humanoid _humanoid;

	private ZNetView _nview;

	private Door _door;

	private float _start;

	// 1.5.8: the order is to close it (it was open when you pointed at it) - the command only ever opened doors, so on an
	// open one it answered and did nothing (user: "They should still close a door when commanded in a dungeon")
	private bool _close;

	public bool IsActive => _door != null;

	// 1.4.150: a new order replaces the door order (it used to keep steering for up to 60 s)
	public void Cancel()
	{
		if (_door != null)
		{
			_door = null;
			if (_ai != null)
			{
				MHR_Mover.Release(_ai);
			}
		}
	}

	public void DirectUseDoor(Door door, bool close)
	{
		if (door == null)
		{
			return;
		}
		_close = close;
		if (!IsUsableDoor(door) || IsOpen(door) != close)
		{
			if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug("[Door] \"" + door.m_name + "\" is locked or already " + (close ? "shut" : "open") + " - nothing to do");
			return;
		}
		_door = door;
		_start = Time.time;
		_ai?.SetFollowTarget(null);
		if (_ai != null)
		{
			MHR_Mover.Release(_ai);
		}
		if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug("[Door] Going to " + (close ? "close" : "open") + " the " + door.m_name + " at " + MHR_Mover.Fmt(door.transform.position) + " (" + Vector3.Distance(base.transform.position, door.transform.position).ToString("F1") + " m away)");
	}

	private void Awake()
	{
		_ai = GetComponent<CompanionAI>();
		_humanoid = GetComponent<Humanoid>();
		_nview = GetComponent<ZNetView>();
	}

	private void Update()
	{
		if (_door == null || _ai == null || _humanoid == null || _nview == null || !_nview.IsValid() || !_nview.IsOwner())
		{
			return;
		}
		if (!IsUsableDoor(_door) || IsOpen(_door) != _close)
		{
			Done(_close ? "the door is shut now" : "the door is open now");
			return;
		}
		if (Time.time - _start > 60f)
		{
			Done("couldn't get to it");
			return;
		}
		// 1.4.150: a fight, picking up loot, a tool trip or its panel open comes first (they steer too)
		if (_ai.IsInCombat || MHR_Loot.Active(this) || MHR_ToolKeeper.IsBusy(this) || CompanionInteractPanel.IsOpenFor(GetComponent<CompanionSetup>()) || CompanionRadialMenu.IsOpenFor(GetComponent<CompanionSetup>()))
		{
			_start += Time.deltaTime;
			return;
		}
		Vector3 me = base.transform.position;
		Vector3 at = _door.transform.position;
		if (MHR_Nav.DoorDist(me, at) > 2.0f)
		{
			MHR_Mover.Go(_ai, Time.deltaTime, at, 1.6f, true, "Door");
			if (MHR_Mover.GaveUp(_ai))
			{
				Done("no way to get to it");
			}
			return;
		}
		_ai.StopMoving();
		MHR_Mover.Release(_ai);
		Vector3 to = at - me;
		to.y = 0f;
		if (to.sqrMagnitude > 0.01f)
		{
			base.transform.rotation = Quaternion.LookRotation(to.normalized);
		}
		bool ok = false;
		try
		{
			ok = _door.Interact(_humanoid, hold: false, alt: false);
		}
		catch
		{
		}
		Done(ok ? (_close ? "closed it" : "opened it") : (_close ? "couldn't close it" : "couldn't open it"));
	}

	private void Done(string why)
	{
		if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug("[Door] " + (_door != null ? _door.m_name : "Door") + ": " + why);
		_door = null;
		_ai?.StopMoving();
		if (_ai != null)
		{
			MHR_Mover.Release(_ai);
		}
		GetComponent<CompanionSetup>()?.ApplyFollowMode((_nview?.GetZDO())?.GetInt(CompanionSetup.ActionModeHash) ?? 0, force: true);
	}

	internal static bool IsOpen(Door door)
	{
		ZNetView nv = door != null ? door.GetComponent<ZNetView>() : null;
		return nv != null && nv.GetZDO() != null && nv.GetZDO().GetInt(ZDOVars.s_state) != 0;
	}

	// a door a companion may use, open or shut: no key needed, not behind someone else's ward
	internal static bool IsUsableDoor(Door door)
	{
		if (door == null || !door || door.m_keyItem != null)
		{
			return false;
		}
		ZNetView nv = door.GetComponent<ZNetView>();
		if (nv == null || nv.GetZDO() == null)
		{
			return false;
		}
		return !door.m_checkGuardStone || PrivateArea.CheckAccess(door.transform.position, 0f, flash: false);
	}

	internal static bool IsValidClosedDoor(Door door)
	{
		if (door == null || !door)
		{
			return false;
		}
		ZNetView component = door.GetComponent<ZNetView>();
		if (component == null || component.GetZDO() == null)
		{
			return false;
		}
		if (component.GetZDO().GetInt(ZDOVars.s_state) != 0)
		{
			return false;
		}
		if (door.m_keyItem != null)
		{
			return false;
		}
		if (door.m_checkGuardStone && !PrivateArea.CheckAccess(door.transform.position, 0f, flash: false))
		{
			return false;
		}
		return true;
	}
}
