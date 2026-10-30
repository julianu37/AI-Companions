using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using HarmonyLib;

namespace Companions;

internal static class EquipDiag
{
	private static int _equipLogs;

	private static int _unequipLogs;

	private static int _hideLogs;

	private static int _scanLogs;

	private static bool _installed;

	private static FieldInfo _rightField;

	private static FieldInfo _hiddenRightField;

	internal static void Install(Harmony harmony)
	{
		if (_installed || harmony == null)
		{
			return;
		}
		_installed = true;
		try
		{
			Type typeFromHandle = typeof(Harmony);
			Type type = typeFromHandle.Assembly.GetType("HarmonyLib.HarmonyMethod");
			MethodInfo methodInfo = null;
			MethodInfo[] methods = typeFromHandle.GetMethods(BindingFlags.Instance | BindingFlags.Public);
			for (int i = 0; i < methods.Length; i++)
			{
				if (methods[i].Name == "Patch" && methods[i].GetParameters().Length >= 3)
				{
					methodInfo = methods[i];
					break;
				}
			}
			if (methodInfo == null || type == null)
			{
				if (CompanionsPlugin.LogDetailed) Info("[EQUIPDIAG] Harmony Patch API not found; trace disabled.");
				return;
			}
			PatchOne(harmony, typeFromHandle, type, methodInfo, "Humanoid", "EquipItem", null, "EquipPostfix");
			PatchOne(harmony, typeFromHandle, type, methodInfo, "Humanoid", "UnequipItem", "UnequipPrefix", null);
			PatchOne(harmony, typeFromHandle, type, methodInfo, "Humanoid", "HideHandItems", "HidePrefix", null);
			PatchOne(harmony, typeFromHandle, type, methodInfo, "Humanoid", "ShowHandItems", "ShowPrefix", null);
			if (CompanionsPlugin.LogDetailed) Info("[EQUIPDIAG] equip tracing installed.");
			ContainerFix.Install(harmony);
		}
		catch (Exception ex)
		{
			if (CompanionsPlugin.LogDetailed) Info("[EQUIPDIAG] install failed: " + ex.Message);
		}
	}

	internal static void OnScan(IList items, ItemDrop.ItemData best1H)
	{
		if (!CompanionsPlugin.LogDetailed)
		{
			return;
		}
		try
		{
			if (best1H != null || _scanLogs >= 12)
			{
				return;
			}
			_scanLogs++;
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("[EQUIPDIAG] AutoEquipBest found no 1H weapon; inventory items=");
			stringBuilder.Append((items != null) ? $"{items.Count}" : "NULL");
			if (items != null)
			{
				int num = items.Count;
				if (num > 10)
				{
					num = 10;
				}
				for (int i = 0; i < num; i++)
				{
					ItemDrop.ItemData itemData = (ItemDrop.ItemData)items[i];
					stringBuilder.Append("\n  [").Append($"{i}").Append("] ")
						.Append(Name(itemData));
					if (itemData != null && itemData.m_shared != null)
					{
						stringBuilder.Append($" type={itemData.m_shared.m_itemType} useDur={itemData.m_shared.m_useDurability} dur={itemData.m_durability} anim={itemData.m_shared.m_animationState} equipped={itemData.m_equipped}");
					}
				}
			}
			if (CompanionsPlugin.LogDetailed) Info(stringBuilder.ToString());
		}
		catch (Exception)
		{
		}
	}

	private static void Info(string s)
	{
		if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log?.LogInfo(s);
	}

	private static bool IsCompanion(Humanoid h)
	{
		if (h == null)
		{
			return false;
		}
		BaseAI baseAI = h.GetBaseAI();
		if (baseAI == null)
		{
			return false;
		}
		return baseAI.GetType().Name == "CompanionAI";
	}

	private static string Name(ItemDrop.ItemData it)
	{
		if (it == null)
		{
			return "none";
		}
		if (it.m_shared == null)
		{
			return "?";
		}
		return it.m_shared.m_name;
	}

	private static string Slots(Humanoid h)
	{
		try
		{
			BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			if (_rightField == null)
			{
				_rightField = typeof(Humanoid).GetField("m_rightItem", bindingAttr);
			}
			if (_hiddenRightField == null)
			{
				_hiddenRightField = typeof(Humanoid).GetField("m_hiddenRightItem", bindingAttr);
			}
			ItemDrop.ItemData it = ((!(_rightField == null)) ? ((ItemDrop.ItemData)_rightField.GetValue(h)) : null);
			ItemDrop.ItemData it2 = ((!(_hiddenRightField == null)) ? ((ItemDrop.ItemData)_hiddenRightField.GetValue(h)) : null);
			return "right=" + Name(it) + " hiddenRight=" + Name(it2);
		}
		catch (Exception ex)
		{
			return "(slots failed: " + ex.Message + ")";
		}
	}

	private static string Stack()
	{
		try
		{
			StackTrace stackTrace = new StackTrace(2, fNeedFileInfo: false);
			StringBuilder stringBuilder = new StringBuilder();
			int num = stackTrace.FrameCount;
			if (num > 7)
			{
				num = 7;
			}
			for (int i = 0; i < num; i++)
			{
				MethodBase method = stackTrace.GetFrame(i).GetMethod();
				if (!(method == null))
				{
					if (stringBuilder.Length > 0)
					{
						stringBuilder.Append(" < ");
					}
					stringBuilder.Append((!(method.DeclaringType == null)) ? method.DeclaringType.Name : "?").Append(".").Append(method.Name);
				}
			}
			return stringBuilder.ToString();
		}
		catch (Exception)
		{
			return "(no stack)";
		}
	}

	private static void EquipPostfix(Humanoid __instance, ItemDrop.ItemData item, bool __result)
	{
		if (!CompanionsPlugin.LogDetailed)
		{
			return;
		}
		try
		{
			if (_equipLogs < 60 && IsCompanion(__instance))
			{
				_equipLogs++;
				if (CompanionsPlugin.LogDetailed) Info("[EQUIPDIAG] EquipItem(" + Name(item) + ") -> " + ((!__result) ? "FALSE" : "true") + " | " + Slots(__instance) + " | itemEquippedFlag=" + ((item == null || !item.m_equipped) ? "false" : "true") + " | " + Stack());
			}
		}
		catch (Exception)
		{
		}
	}

	private static void UnequipPrefix(Humanoid __instance, ItemDrop.ItemData item)
	{
		if (!CompanionsPlugin.LogDetailed)
		{
			return;
		}
		try
		{
			if (_unequipLogs < 60 && IsCompanion(__instance))
			{
				_unequipLogs++;
				if (CompanionsPlugin.LogDetailed) Info("[EQUIPDIAG] UnequipItem(" + Name(item) + ") | " + Slots(__instance) + " | " + Stack());
			}
		}
		catch (Exception)
		{
		}
	}

	private static void HidePrefix(Humanoid __instance)
	{
		if (!CompanionsPlugin.LogDetailed)
		{
			return;
		}
		try
		{
			if (_hideLogs < 40 && IsCompanion(__instance))
			{
				_hideLogs++;
				if (CompanionsPlugin.LogDetailed) Info("[EQUIPDIAG] HideHandItems | " + Slots(__instance) + " | " + Stack());
			}
		}
		catch (Exception)
		{
		}
	}

	private static void ShowPrefix(Humanoid __instance)
	{
		if (!CompanionsPlugin.LogDetailed)
		{
			return;
		}
		try
		{
			if (_hideLogs < 40 && IsCompanion(__instance))
			{
				_hideLogs++;
				if (CompanionsPlugin.LogDetailed) Info("[EQUIPDIAG] ShowHandItems | " + Slots(__instance) + " | " + Stack());
			}
		}
		catch (Exception)
		{
		}
	}

	private static void PatchOne(object harmony, Type htype, Type hmType, MethodInfo patchMethod, string typeName, string methodName, string prefixName, string postfixName)
	{
		try
		{
			BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			MethodInfo method = typeof(Humanoid).GetMethod(methodName, bindingAttr);
			if (method == null)
			{
				if (CompanionsPlugin.LogDetailed) Info("[EQUIPDIAG] patch skipped, method not found: " + methodName);
				return;
			}
			ParameterInfo[] parameters = patchMethod.GetParameters();
			object[] array = new object[parameters.Length];
			array[0] = method;
			for (int i = 1; i < parameters.Length; i++)
			{
				string name = parameters[i].Name;
				if (name == "prefix" && prefixName != null)
				{
					array[i] = Activator.CreateInstance(hmType, typeof(EquipDiag).GetMethod(prefixName, bindingAttr));
				}
				else if (name == "postfix" && postfixName != null)
				{
					array[i] = Activator.CreateInstance(hmType, typeof(EquipDiag).GetMethod(postfixName, bindingAttr));
				}
			}
			patchMethod.Invoke(harmony, array);
		}
		catch (Exception ex)
		{
			if (CompanionsPlugin.LogDetailed) Info("[EQUIPDIAG] patch failed for " + methodName + ": " + ex.Message);
		}
	}
}
