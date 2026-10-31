using System;
using System.Reflection;

namespace Companions;

internal static class GridFix
{
	private static MethodInfo _canDrop;

	private static bool _logged;

	internal static void EnsureCanDrop(InventoryGrid grid)
	{
		if (grid == null || grid.CanDropDragOntoItem != null)
		{
			return;
		}
		InventoryGui instance = InventoryGui.instance;
		if (instance == null)
		{
			return;
		}
		if (_canDrop == null)
		{
			_canDrop = typeof(InventoryGui).GetMethod("CanDropDragOntoItem", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		}
		if (!(_canDrop == null))
		{
			grid.CanDropDragOntoItem = (Func<ItemDrop.ItemData, bool>)Delegate.CreateDelegate(typeof(Func<ItemDrop.ItemData, bool>), instance, _canDrop);
			if (!_logged)
			{
				_logged = true;
				if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log?.LogInfo("[GridFix] Bound vanilla InventoryGui.CanDropDragOntoItem to the companion grid.");
			}
		}
	}
}
