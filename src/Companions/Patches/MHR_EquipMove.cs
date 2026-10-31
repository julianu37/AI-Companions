using System.Collections.Generic;

namespace Companions;

public static class MHR_EquipMove
{
	internal static void UnequipIfFromCompanion(Inventory target, Inventory fromInventory, ItemDrop.ItemData item)
	{
		if (item == null || fromInventory == null || target == fromInventory || !item.m_equipped)
		{
			return;
		}
		IReadOnlyList<CompanionSetup> allCompanions = CompanionSetup.AllCompanions;
		if (allCompanions == null)
		{
			return;
		}
		for (int i = 0; i < allCompanions.Count; i++)
		{
			CompanionSetup companionSetup = allCompanions[i];
			if (!(companionSetup == null))
			{
				Humanoid component = companionSetup.GetComponent<Humanoid>();
				if (!(component == null) && component.GetInventory() == fromInventory)
				{
					component.UnequipItem(item, triggerEquipEffects: false);
					item.m_equipped = false;
					if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo($"[Inventory] Unequipped \"{item.m_shared.m_name}\" as it left the companion's inventory");
					break;
				}
			}
		}
	}
}
