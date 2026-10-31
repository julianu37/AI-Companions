using System.Collections.Generic;
using UnityEngine;

namespace Companions;

// 1.5.44 (GitHub issue #17, request 2): a key per stance ([Controls] PassiveKey / BalancedKey / RangedKey / MeleeKey,
// unbound by default). Looking at one of your companions: just that one; otherwise every companion of yours within
// RecallRange that has Command on (user: aim to pick rather than a key to cycle through them). A message top left says
// who changed.
internal static class MHR_StanceKeys
{
	private static readonly List<string> s_names = new List<string>();

	internal static void Tick(Player player)
	{
		if (player == null)
		{
			return;
		}
		int stance = -1;
		if ((Pressed(ModConfig.PassiveKey) || (ZInput.IsGamepadActive() && MHR_Pad.Down(ModConfig.PadPassive, ModConfig.PadPassiveModifier))))
		{
			stance = CompanionSetup.StancePassive;
		}
		else if ((Pressed(ModConfig.BalancedKey) || (ZInput.IsGamepadActive() && MHR_Pad.Down(ModConfig.PadBalanced, ModConfig.PadBalancedModifier))))
		{
			stance = CompanionSetup.StanceBalanced;
		}
		else if ((Pressed(ModConfig.RangedKey) || (ZInput.IsGamepadActive() && MHR_Pad.Down(ModConfig.PadRanged, ModConfig.PadRangedModifier))))
		{
			stance = CompanionSetup.StanceRanged;
		}
		else if ((Pressed(ModConfig.MeleeKey) || (ZInput.IsGamepadActive() && MHR_Pad.Down(ModConfig.PadMelee, ModConfig.PadMeleeModifier))))
		{
			stance = CompanionSetup.StanceMelee;
		}
		if (stance < 0)
		{
			return;
		}
		s_names.Clear();
		CompanionSetup aimed = player.GetHoverObject()?.GetComponentInParent<CompanionSetup>();
		if (aimed != null && MHR_Owner.IsMine(aimed))
		{
			Set(aimed, stance);
		}
		else
		{
			float range = ModConfig.RecallRange?.Value ?? 40f;
			foreach (CompanionSetup cs in CompanionSetup.AllCompanions)
			{
				if (cs == null || !MHR_Owner.IsMine(cs) || !cs.GetIsCommandable())
				{
					continue;
				}
				Character c = cs.GetComponent<Character>();
				if (c == null || c.IsDead() || (range > 0f && Vector3.Distance(cs.transform.position, player.transform.position) > range))
				{
					continue;
				}
				Set(cs, stance);
			}
		}
		string label = ModLocalization.Loc(stance == CompanionSetup.StancePassive ? "hc_radial_passive" : stance == CompanionSetup.StanceRanged ? "hc_radial_ranged" : stance == CompanionSetup.StanceMelee ? "hc_radial_melee" : "hc_radial_balanced");
		string msg = (s_names.Count > 0) ? ModLocalization.LocFmt("hc_msg_stance_set", string.Join(", ", s_names.ToArray()), label) : ModLocalization.LocFmt("hc_msg_stance_none", label);
		MessageHud.instance?.ShowMessage(MessageHud.MessageType.TopLeft, msg);
		if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Stance] " + msg);
	}

	private static bool Pressed(BepInEx.Configuration.ConfigEntry<KeyCode> key)
	{
		return key != null && key.Value != KeyCode.None && Input.GetKeyDown(key.Value);
	}

	private static void Set(CompanionSetup cs, int stance)
	{
		cs.SetCombatStance(stance);
		ZDO zdo = cs.GetComponent<ZNetView>()?.GetZDO();
		s_names.Add((zdo != null) ? zdo.GetString(CompanionSetup.NameHash, "Companion") : "Companion");
	}
}
