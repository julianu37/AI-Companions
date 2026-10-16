using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Companions;

public static class SleepPatches
{
	[HarmonyPatch(typeof(Game), "EverybodyIsTryingToSleep")]
	private static class EverybodyIsTryingToSleep_Patch
	{
		private static void Postfix(ref bool __result)
		{
			if (!__result)
			{
				return;
			}
			IReadOnlyList<CompanionRest> instances = CompanionRest.Instances;
			if (instances.Count == 0)
			{
				return;
			}
			for (int i = 0; i < instances.Count; i++)
			{
				CompanionRest companionRest = instances[i];
				if (!(companionRest == null) && MHR_ShouldWaitFor(companionRest) && !companionRest.IsSleeping)
				{
					__result = false;
					break;
				}
			}
		}
	}

	[HarmonyPatch(typeof(Game), "SleepStop")]
	private static class SleepStop_Patch
	{
		private static void Postfix()
		{
			CompanionRest.MHR_ClearStayAwakeAll();
			IReadOnlyList<CompanionRest> instances = CompanionRest.Instances;
			for (int num = instances.Count - 1; num >= 0; num--)
			{
				CompanionRest companionRest = instances[num];
				if (companionRest != null && companionRest.IsSleeping)
				{
					companionRest.CancelDirected();
					companionRest.MHR_SuppressAutoSleep(120f);
				}
			}
		}
	}

	[HarmonyPatch(typeof(Game), "UpdateSleeping")]
	private static class UpdateSleeping_Patch
	{
		private static void Postfix()
		{
			Player localPlayer = Player.m_localPlayer;
			// 1.4.150: only the host's game holds the night skip for companions - elsewhere the message was wrong
			if (localPlayer == null || !localPlayer.InBed() || ZNet.instance == null || !ZNet.instance.IsServer())
			{
				return;
			}
			IReadOnlyList<CompanionRest> instances = CompanionRest.Instances;
			if (instances.Count == 0)
			{
				return;
			}
			bool flag = true;
			string text = null;
			for (int i = 0; i < instances.Count; i++)
			{
				CompanionRest companionRest = instances[i];
				if (!(companionRest == null) && MHR_ShouldWaitFor(companionRest) && !companionRest.IsSleeping)
				{
					flag = false;
					text = GetCompanionName(companionRest);
					break;
				}
			}
			if (!flag)
			{
				_messageTimer -= Time.deltaTime;
				if (!(_messageTimer > 0f))
				{
					_messageTimer = 3f;
					string msg = ((text != null) ? ModLocalization.LocFmt("hc_msg_sleep_waiting", text) : ModLocalization.Loc("hc_msg_sleep_waiting_generic"));
					localPlayer.Message(MessageHud.MessageType.Center, msg);
				}
			}
		}
	}

	private const float MessageInterval = 3f;

	private static float _messageTimer;

	private static float s_mhrBedSince;

	internal static bool MHR_ShouldWaitFor(CompanionRest rest)
	{
		if (!(rest == null))
		{
			if (!CompanionRest.MHR_LocalPlayerInBed())
			{
				s_mhrBedSince = 0f;
			}
			else
			{
				if (s_mhrBedSince <= 0f)
				{
					s_mhrBedSince = Time.time;
				}
				if (Time.time - s_mhrBedSince <= 30f)
				{
					return rest.MHR_WantsBedNow();
				}
			}
		}
		return false;
	}

	private static bool IsFollowMode(CompanionRest rest)
	{
		ZNetView component = rest.GetComponent<ZNetView>();
		if (component == null || component.GetZDO() == null)
		{
			return false;
		}
		int num = component.GetZDO().GetInt(CompanionSetup.ActionModeHash);
		return num == 0;
	}

	private static string GetCompanionName(CompanionRest rest)
	{
		ZNetView component = rest.GetComponent<ZNetView>();
		if (component != null && component.GetZDO() != null)
		{
			string text = component.GetZDO().GetString(CompanionSetup.NameHash);
			if (!string.IsNullOrEmpty(text))
			{
				return text;
			}
		}
		Character component2 = rest.GetComponent<Character>();
		if (!(component2 != null))
		{
			return ModLocalization.Loc("hc_msg_name_default");
		}
		return component2.m_name;
	}
}
