using System;
using System.Collections;
using UnityEngine;

namespace Companions;

internal static class AIGateDiag
{
	private static readonly Hashtable _lastGate = new Hashtable();

	private static readonly Hashtable _lastHeartbeat = new Hashtable();

	private static int _logs;

	internal static void Report(Component aiComp, string gateOrNull)
	{
		try
		{
			if (aiComp == null)
			{
				return;
			}
			int instanceID = aiComp.GetInstanceID();
			object obj = _lastGate[instanceID];
			string text = obj as string;
			if (gateOrNull == null)
			{
				if (text != null)
				{
					Log("[AIGate] \"" + aiComp.name + "\" resumed normal AI (was blocked by " + text + ")");
					_lastGate.Remove(instanceID);
					_lastHeartbeat.Remove(instanceID);
				}
				return;
			}
			if (text == null || text != gateOrNull)
			{
				Log("[AIGate] \"" + aiComp.name + "\" blocked by " + gateOrNull);
				_lastGate[instanceID] = gateOrNull;
				_lastHeartbeat[instanceID] = Time.time;
				return;
			}
			object obj2 = _lastHeartbeat[instanceID];
			float num = ((obj2 == null) ? 0f : ((float)obj2));
			if (Time.time - num >= 15f)
			{
				Log("[AIGate] \"" + aiComp.name + "\" still blocked by " + gateOrNull + " (" + (Time.time - num).ToString("F0") + "s+)");
				_lastHeartbeat[instanceID] = Time.time;
			}
		}
		catch (Exception)
		{
		}
	}

	private static void Log(string s)
	{
		if (_logs < 200)
		{
			_logs++;
			CompanionsPlugin.Log?.LogDebug(s);
		}
	}
}
