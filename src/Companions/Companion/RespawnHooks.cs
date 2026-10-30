using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Companions;

internal static class RespawnHooks
{
	private static int _logs;

	private static Type _setupType;

	private static MethodInfo _allCompanionsM;

	private static MethodInfo _restoreFollowTargetM;

	private static MethodInfo _ignoreCollisionM;

	internal static void FullRestore()
	{
		try
		{
			if (_setupType == null)
			{
				_setupType = Type.GetType("Companions.CompanionSetup");
			}
			if (_setupType == null)
			{
				return;
			}
			if (_allCompanionsM == null)
			{
				_allCompanionsM = _setupType.GetMethod("get_AllCompanions", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			}
			if (_allCompanionsM == null)
			{
				return;
			}
			if (_restoreFollowTargetM == null)
			{
				_restoreFollowTargetM = _setupType.GetMethod("RestoreFollowTarget", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}
			if (!(_allCompanionsM.Invoke(null, null) is IEnumerable enumerable))
			{
				return;
			}
			foreach (object item in enumerable)
			{
				try
				{
					if (_restoreFollowTargetM != null)
					{
						_restoreFollowTargetM.Invoke(item, null);
					}
				}
				catch (Exception)
				{
				}
				Component component = item as Component;
				if (component == null)
				{
					continue;
				}
				Component component2 = component.GetComponent("CompanionAI");
				if (!(component2 != null))
				{
					continue;
				}
				if (_ignoreCollisionM == null)
				{
					_ignoreCollisionM = component2.GetType().GetMethod("IgnorePlayerCollision", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				}
				try
				{
					if (_ignoreCollisionM != null)
					{
						_ignoreCollisionM.Invoke(component2, null);
					}
				}
				catch (Exception)
				{
				}
			}
			ReturnLogic.OnPlayerRespawnFrom("OnRespawn");
		}
		catch (Exception ex3)
		{
			if (CompanionsPlugin.LogNormal) Log("[Respawn] FullRestore failed: " + ex3.Message);
		}
	}

	private static void Log(string s)
	{
		if (_logs < 20)
		{
			_logs++;
			if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log?.LogInfo(s);
		}
	}
}
