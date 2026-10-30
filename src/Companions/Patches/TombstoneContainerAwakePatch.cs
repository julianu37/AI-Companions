using HarmonyLib;

namespace Companions;

[HarmonyPatch(typeof(Container), "Awake")]
public static class TombstoneContainerAwakePatch
{
	[HarmonyPostfix]
	private static void Postfix(Container __instance)
	{
		TombStone component = __instance.GetComponent<TombStone>();
		if (component == null)
		{
			return;
		}
		ZDO zDO = __instance.GetComponent<ZNetView>()?.GetZDO();
		if (zDO == null)
		{
			return;
		}
		int num = zDO.GetInt(CompanionSetup.TombInvWidthHash);
		int num2 = zDO.GetInt(CompanionSetup.TombInvHeightHash);
		if (num <= 0 || num2 <= 0)
		{
			return;
		}
		Inventory inventory = __instance.GetInventory();
		if (inventory == null)
		{
			return;
		}
		int width = inventory.GetWidth();
		int height = inventory.GetHeight();
		if (width != num || height != num2)
		{
			if (ContainerAwakePatch._invWidthField != null)
			{
				ContainerAwakePatch._invWidthField.SetValue(inventory, num);
			}
			if (ContainerAwakePatch._invHeightField != null)
			{
				ContainerAwakePatch._invHeightField.SetValue(inventory, num2);
			}
			if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug($"[TombstoneAwake] Restored companion tombstone inventory dims: {width}x{height} -> {num}x{num2}");
		}
	}
}
