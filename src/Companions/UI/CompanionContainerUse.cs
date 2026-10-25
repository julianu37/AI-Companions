using System;
using System.Reflection;
using HarmonyLib;

namespace Companions;

/// <summary>
/// The game's own "someone has this pack open" bookkeeping for the companion inventory window.
///
/// <para>The mod opens that window itself (<see cref="CompanionInteract.OpenCompanionInventory"/>) instead of going
/// through <c>Container.Interact</c> -&gt; <c>RPC_RequestOpen</c>, so nothing used to set <c>Container.m_inUse</c>:
/// two players could edit the same companion pack at the same time and only the game that owns the ZDO saves it, so
/// the other one's edits were lost (or items ended up duplicated). This mirrors what the jobs already do for chests
/// (<c>MHR_ToolKeeper.Chests.InUse</c>/<c>Open</c>/<c>Close</c>): refuse a pack that is in use, mark it in use while
/// the window is open and clear the flag on every way out.</para>
/// </summary>
internal static class CompanionContainerUse
{
	/// <summary>The container this game marked as in use (null when no companion window is open).</summary>
	private static Container s_open;

	/// <summary>
	/// True when someone has this pack open: the local flag when this game owns the container, the synced ZDO flag
	/// otherwise (<c>Container.IsInUse</c> only knows the local copy, and only the owner writes the ZDO flag from it).
	/// </summary>
	internal static bool InUse(Container container)
	{
		if (container == null)
		{
			return false;
		}
		if (container == s_open)
		{
			// it is *our* window: pressing interact again (or the AI order arriving twice) has to re-show it, not
			// be told the pack is in use
			return false;
		}
		if (container.IsInUse())
		{
			return true;
		}
		ZNetView nview = container.GetComponent<ZNetView>();
		return nview != null && nview.IsValid() && !nview.IsOwner() && nview.GetZDO().GetInt(ZDOVars.s_inUse) == 1;
	}

	/// <summary>
	/// Marks the pack as in use, like the game does when it shows a container. <c>Container.SetInUse</c> only works on
	/// the ZDO owner (it writes the synced flag from there), so this does nothing when the ownership is not ours -
	/// the caller only gets here after the claim.
	/// </summary>
	internal static void Mark(Container container)
	{
		if (container == null || s_open == container)
		{
			return;
		}
		Release();
		ZNetView nview = container.GetComponent<ZNetView>();
		if (nview != null && nview.IsValid() && !nview.IsOwner())
		{
			return;
		}
		s_open = container;
		container.SetInUse(true);
	}

	/// <summary>
	/// Clears the flag and forgets the container. Called on every way the window closes (the mod panel's
	/// <c>HideInternal</c>, its <c>OnDestroy</c> and a switch to another container) plus the game's own
	/// <c>InventoryGui.Hide</c>, which clears it by itself: a flag left behind is worse than the bug it fixes, because
	/// then nobody can open the pack again.
	/// </summary>
	internal static void Release()
	{
		Container container = s_open;
		s_open = null;
		if (container == null)
		{
			return;
		}
		ZNetView nview = container.GetComponent<ZNetView>();
		if (nview != null && nview.IsValid() && !nview.IsOwner())
		{
			CompanionsPlugin.Log.LogWarning("[Companion] The inventory window closed without the container's ownership - its \"in use\" flag may stay set");
			return;
		}
		container.SetInUse(false);
	}

	// ---- Fase 3.24 (auditoria): the flag a client that went away left behind ----

	/// <summary>How long the flag has to stay unexplained before it is released.</summary>
	private const float StaleSeconds = 5f;

	/// <summary><c>InventoryGui.m_currentContainer</c>: private in the game, read to know which window is open.</summary>
	private static readonly FieldInfo _currentContainerField = AccessTools.Field(typeof(InventoryGui), "m_currentContainer");

	/// <summary>The container the game is showing right now, or null when no container window is open.</summary>
	private static Container CurrentGameContainer()
	{
		InventoryGui gui = InventoryGui.instance;
		if (gui == null || _currentContainerField == null)
		{
			return null;
		}
		try
		{
			return _currentContainerField.GetValue(gui) as Container;
		}
		catch (Exception)
		{
			return null;
		}
	}

	/// <summary>
	/// Is this pack carrying an "in use" flag that nobody is going to release? Only the ZDO owner writes the flag, so
	/// a client that dies with the pack open (a crash, alt-F4 - no <c>Hide</c>) leaves it set for everyone else and the
	/// pack stays refused for good. The owner side never reads its own flag back (<c>Container.m_inUse</c> is only set
	/// by <c>SetInUse</c>), so a window of this game is never a reason for the flag to be set - that is
	/// <see cref="s_open"/> or the game's own window. Everything below is a cheap null/bool read.
	/// </summary>
	internal static bool IsStaleInUse(Container container)
	{
		if (container == null || container == s_open)
		{
			return false;
		}
		ZNetView nview = container.GetComponent<ZNetView>();
		if (nview == null || !nview.IsValid() || !nview.IsOwner() || container.IsInUse())
		{
			return false;
		}
		ZDO zdo = nview.GetZDO();
		if (zdo == null || zdo.GetInt(ZDOVars.s_inUse) != 1)
		{
			return false;
		}
		return CurrentGameContainer() != container;
	}

	/// <summary>
	/// Fase 3.24 (auditoria): releases a flag that has been <see cref="IsStaleInUse"/> for <see cref="StaleSeconds"/>.
	/// <paramref name="since"/> is the caller's stamp (one per companion, so several companions never fight over a
	/// single candidate) and is reset whenever the pack stops looking stale. Called once a second per companion - the
	/// wait keeps the flag protecting a window that is genuinely open elsewhere, and the flag is only cleared when the
	/// player is not looking at that very pack.
	/// </summary>
	internal static void CheckStale(Container container, ref float since)
	{
		float now = UnityEngine.Time.time;
		if (!IsStaleInUse(container))
		{
			since = -1f;
			return;
		}
		if (since < 0f)
		{
			since = now;
			return;
		}
		if (now - since < StaleSeconds)
		{
			return;
		}
		since = -1f;
		ZNetView nview = container.GetComponent<ZNetView>();
		ZDO zdo = nview != null ? nview.GetZDO() : null;
		if (zdo == null)
		{
			return;
		}
		// the same write Container.UpdateUseVisual does, without its private call (m_inUse is already false here)
		zdo.Set(ZDOVars.s_inUse, 0);
		CompanionsPlugin.Log.LogWarning("[Companion] A pack was left marked \"in use\" by a client that went away - the flag was cleared so it can be opened again");
	}
}
