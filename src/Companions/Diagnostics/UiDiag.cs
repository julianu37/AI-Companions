using System;
using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Companions;

internal static class UiDiag
{
	private static int _logs;

	private static FieldInfo _containerGridField;

	private static FieldInfo _elementsField;

	private static void Log(string s)
	{
		if (_logs < 20)
		{
			_logs++;
			if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log?.LogInfo(s);
		}
	}

	internal static void LogContainerSwitch(InventoryGui gui, Container previousContainer, Container newContainer)
	{
		try
		{
			if (gui == null || !(previousContainer != null) || !(previousContainer != newContainer))
			{
				return;
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("[UiDiag] switching container: prev=").Append(previousContainer.name);
			stringBuilder.Append(" (destroyed=").Append((!(previousContainer == null)) ? "false" : "true").Append(")");
			stringBuilder.Append(" -> new=").Append((!(newContainer == null)) ? newContainer.name : "null");
			if (_containerGridField == null)
			{
				_containerGridField = typeof(InventoryGui).GetField("m_containerGrid", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}
			InventoryGrid inventoryGrid = ((!(_containerGridField == null)) ? ((InventoryGrid)_containerGridField.GetValue(gui)) : null);
			if (inventoryGrid == null)
			{
				stringBuilder.Append(" | vanilla m_containerGrid: NULL");
			}
			else
			{
				GameObject gameObject = inventoryGrid.gameObject;
				stringBuilder.Append(" | vanilla m_containerGrid.gameObject active=").Append((!gameObject.activeInHierarchy) ? "false" : "true").Append(" (self=")
					.Append((!gameObject.activeSelf) ? "false" : "true")
					.Append(")");
				if (_elementsField == null)
				{
					_elementsField = typeof(InventoryGrid).GetField("m_elements", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				}
				IList list = ((!(_elementsField == null)) ? ((IList)_elementsField.GetValue(inventoryGrid)) : null);
				int num = 0;
				if (list != null)
				{
					for (int i = 0; i < list.Count; i++)
					{
						Component component = list[i] as Component;
						if (component != null && component.gameObject.activeSelf)
						{
							num++;
						}
					}
					stringBuilder.Append(" | vanilla grid elements=").Append($"{list.Count}").Append(" visibleSelf=")
						.Append($"{num}");
				}
			}
			if (CompanionsPlugin.LogDetailed) Log(stringBuilder.ToString());
		}
		catch (Exception ex)
		{
			if (CompanionsPlugin.LogDetailed) Log("[UiDiag] check failed: " + ex.Message);
		}
	}
}
