using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;

namespace Companions;

internal static class ContainerFix
{
	private static FieldInfo _nview;

	private static FieldInfo _lastRev;

	private static Hashtable _owned = new Hashtable();

	private static Hashtable _bytes = new Hashtable();

	private static int _skips;

	private static bool _logged;

	internal static void Install(Harmony harmony)
	{
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
				if (CompanionsPlugin.LogDetailed) Log("[ContainerFix] Harmony Patch API not found.");
				return;
			}
			PatchOne(harmony, methodInfo, type, "Load", "LoadPrefix", "LoadPostfix");
			PatchOne(harmony, methodInfo, type, "Save", null, "SavePostfix");
			if (CompanionsPlugin.LogDetailed) Log("[ContainerFix] installed.");
		}
		catch (Exception ex)
		{
			if (CompanionsPlugin.LogDetailed) Log("[ContainerFix] install failed: " + ex.Message);
		}
	}

	private static bool IsCompanionContainer(Container c)
	{
		return c != null && c.GetComponent(typeof(Humanoid)) != null;
	}

	private static ZNetView View(Container c)
	{
		if (_nview == null)
		{
			_nview = typeof(Container).GetField("m_nview", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		}
		return (!(_nview == null)) ? ((ZNetView)_nview.GetValue(c)) : null;
	}

	private static bool SameBytes(byte[] a, byte[] b)
	{
		if (a == null || b == null)
		{
			return a == b;
		}
		if (a.Length != b.Length)
		{
			return false;
		}
		for (int i = 0; i < a.Length; i++)
		{
			if (a[i] != b[i])
			{
				return false;
			}
		}
		return true;
	}

	private static bool LoadPrefix(Container __instance, ref bool __result)
	{
		try
		{
			if (!IsCompanionContainer(__instance))
			{
				return true;
			}
			ZNetView zNetView = View(__instance);
			if (zNetView == null || !zNetView.IsValid())
			{
				return true;
			}
			object key = __instance.GetInstanceID();
			if (!zNetView.IsOwner())
			{
				return true;
			}
			object obj = _owned[key];
			if (obj == null || !(bool)obj)
			{
				return true;
			}
			ZDO zDO = zNetView.GetZDO();
			if (zDO == null)
			{
				return true;
			}
			byte[] byteArray = zDO.GetByteArray(ZDOVars.s_items);
			if (!SameBytes(byteArray, (byte[])_bytes[key]))
			{
				return true;
			}
			if (_lastRev == null)
			{
				_lastRev = typeof(Container).GetField("m_lastRevision", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}
			if (_lastRev != null)
			{
				_lastRev.SetValue(__instance, zDO.DataRevision);
			}
			__result = false;
			_skips++;
			if (!_logged)
			{
				_logged = true;
				if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log?.LogInfo("[ContainerFix] Skipping redundant Container.Load for the companion (items unchanged; ZDO revision bumped by other data).");
			}
			return false;
		}
		catch (Exception)
		{
			return true;
		}
	}

	private static void LoadPostfix(Container __instance, bool __result)
	{
		try
		{
			if (__result && IsCompanionContainer(__instance))
			{
				ZNetView zNetView = View(__instance);
				if (!(zNetView == null) && zNetView.IsValid())
				{
					object key = __instance.GetInstanceID();
					ZDO zDO = zNetView.GetZDO();
					_owned[key] = zNetView.IsOwner();
					_bytes[key] = zDO?.GetByteArray(ZDOVars.s_items);
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private static void SavePostfix(Container __instance)
	{
		try
		{
			if (IsCompanionContainer(__instance))
			{
				ZNetView zNetView = View(__instance);
				if (!(zNetView == null) && zNetView.IsValid() && zNetView.IsOwner())
				{
					object key = __instance.GetInstanceID();
					ZDO zDO = zNetView.GetZDO();
					_owned[key] = true;
					_bytes[key] = zDO?.GetByteArray(ZDOVars.s_items);
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private static void PatchOne(object harmony, MethodInfo patchMethod, Type hmType, string methodName, string prefixName, string postfixName)
	{
		try
		{
			BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			MethodInfo method = typeof(Container).GetMethod(methodName, bindingAttr);
			if (method == null)
			{
				if (CompanionsPlugin.LogDetailed) Log("[ContainerFix] method not found: " + methodName);
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
					array[i] = Activator.CreateInstance(hmType, typeof(ContainerFix).GetMethod(prefixName, bindingAttr));
				}
				else if (name == "postfix" && postfixName != null)
				{
					array[i] = Activator.CreateInstance(hmType, typeof(ContainerFix).GetMethod(postfixName, bindingAttr));
				}
			}
			patchMethod.Invoke(harmony, array);
		}
		catch (Exception ex)
		{
			if (CompanionsPlugin.LogDetailed) Log("[ContainerFix] patch failed for " + methodName + ": " + ex.Message);
		}
	}

	private static void Log(string s)
	{
		if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log?.LogInfo(s);
	}
}
