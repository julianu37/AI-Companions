using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Companions;

// 1.5.77: Odin's Shrine menu (user: "Using the shrine should give a popup that has 2 options. 1. Recover lost companions
// 2. Purchase a new companion ..."). 1.5.80: it IS the game's own popup now - the one the unsummon and quit confirms use -
// with its buttons relabelled (user: "The buttons and background look like they are poorly scaled rather than take from
// the game ... The unsummon option does not have this issue. Same with the quit game menu"; "The font size is two
// different sizes. It should be something close to how the 'ok' button looks"). This component only opens the popups and
// closes them like a chest: Use again, Escape, or walking away.
public class ShrinePanel : MonoBehaviour
{
	private static ShrinePanel _instance;

	private CompanionShrine _shrine;

	// one of our popups is showing
	private bool _active;

	private float _openedAt;

	private const float CloseDistance = 4f;

	private static float s_closedAt = -10f;

	private static readonly AccessTools.FieldRef<UnifiedPopup, TMP_Text> s_leftText = AccessTools.FieldRefAccess<UnifiedPopup, TMP_Text>("buttonLeftText");

	private static readonly AccessTools.FieldRef<UnifiedPopup, TMP_Text> s_rightText = AccessTools.FieldRefAccess<UnifiedPopup, TMP_Text>("buttonRightText");

	private static readonly AccessTools.FieldRef<UnifiedPopup, TMP_Text> s_centerText = AccessTools.FieldRefAccess<UnifiedPopup, TMP_Text>("buttonCenterText");

	private static readonly System.Reflection.FieldInfo s_popupInstance = AccessTools.Field(typeof(UnifiedPopup), "instance");

	internal static bool IsOpen => _instance != null && _instance._active;

	// 1.5.79: the Use press that closes the menu doesn't open it again on the same press
	internal static bool JustClosed => Time.time - s_closedAt < 0.3f;

	internal static void Show(CompanionShrine shrine)
	{
		if (!UnifiedPopup.IsAvailable() || UnifiedPopup.IsVisible())
		{
			return;
		}
		if (_instance == null)
		{
			GameObject gameObject = new GameObject("HC_ShrinePanel");
			Object.DontDestroyOnLoad(gameObject);
			_instance = gameObject.AddComponent<ShrinePanel>();
		}
		_instance._shrine = shrine;
		_instance._openedAt = Time.time;
		_instance.ShowMenu();
	}

	// the two options: Recover companions (left), Summon companion (right)
	private void ShowMenu()
	{
		string text = ModLocalization.Loc("hc_shrine_flavor") + "\n\n" + ModLocalization.LocFmt("hc_shrine_offering", CompanionTierData.Price.ToString("N0"));
		Push(new YesNoPopup(ModLocalization.Loc("hc_shrine_title"), text, Summon, ShowRecover, localizeText: false), ModLocalization.Loc("hc_shrine_btn_summon"), ModLocalization.Loc("hc_shrine_btn_recover_list"), null);
		CompanionsPlugin.Log.LogInfo("[Shrine] Menu opened");
	}

	// each companion on the roster and its status; Recover when one can be brought back, Back to the menu
	private void ShowRecover()
	{
		List<KeyValuePair<MHR_Roster.Entry, MHR_Roster.Status>> roster = MHR_Roster.GetRoster();
		CompanionsPlugin.Log.LogInfo($"[Shrine] Recover list - {roster.Count} companion(s) on the roster");
		List<KeyValuePair<MHR_Roster.Entry, MHR_Roster.Status>> candidates = new List<KeyValuePair<MHR_Roster.Entry, MHR_Roster.Status>>();
		StringBuilder sb = new StringBuilder();
		foreach (KeyValuePair<MHR_Roster.Entry, MHR_Roster.Status> item in roster)
		{
			sb.Append(item.Key.Name).Append(" - ").Append(StatusText(item.Value)).Append('\n');
			if (CanRecover(item.Value))
			{
				candidates.Add(item);
			}
		}
		// Lost first, then Unsummoned, then Unknown
		candidates.Sort((KeyValuePair<MHR_Roster.Entry, MHR_Roster.Status> a, KeyValuePair<MHR_Roster.Entry, MHR_Roster.Status> b) => Order(a.Value).CompareTo(Order(b.Value)));
		string text = roster.Count == 0 ? ModLocalization.Loc("hc_shrine_none") : sb.ToString().TrimEnd('\n');
		string header = ModLocalization.Loc("hc_shrine_recover_title");
		if (candidates.Count == 0)
		{
			Push(new WarningPopup(header, text, ShowMenu, localizeText: false), null, null, ModLocalization.Loc("hc_shrine_back"));
			return;
		}
		Push(new YesNoPopup(header, text, delegate
		{
			AskRecover(candidates, 0);
		}, ShowMenu, localizeText: false), ModLocalization.Loc("hc_shrine_btn_recover"), ModLocalization.Loc("hc_shrine_back"), null);
	}

	private static bool CanRecover(MHR_Roster.Status s)
	{
		return s == MHR_Roster.Status.Lost || s == MHR_Roster.Status.Unknown || s == MHR_Roster.Status.Unsummoned;
	}

	private static int Order(MHR_Roster.Status s)
	{
		return s == MHR_Roster.Status.Lost ? 0 : (s == MHR_Roster.Status.Unsummoned ? 1 : 2);
	}

	private static string StatusText(MHR_Roster.Status s)
	{
		return s switch
		{
			MHR_Roster.Status.Alive => "<color=#80E080>" + ModLocalization.Loc("hc_shrine_alive") + "</color>",
			MHR_Roster.Status.Dead => "<color=#C0C0C0>" + ModLocalization.Loc("hc_shrine_dead") + "</color>",
			MHR_Roster.Status.Lost => "<color=#FF7060>" + ModLocalization.Loc("hc_shrine_lost") + "</color>",
			MHR_Roster.Status.Unsummoned => "<color=#FF9F50>" + ModLocalization.Loc("hc_shrine_unsummoned") + "</color>",
			_ => "<color=#FFCC50>" + ModLocalization.Loc("hc_shrine_unknown") + "</color>",
		};
	}

	// Yes / No for each one that can be brought back (game order: Yes right, No left); No moves on to the next
	private void AskRecover(List<KeyValuePair<MHR_Roster.Entry, MHR_Roster.Status>> candidates, int index)
	{
		if (index >= candidates.Count)
		{
			ShowRecover();
			return;
		}
		MHR_Roster.Entry entry = candidates[index].Key;
		MHR_Roster.Status status = candidates[index].Value;
		string text = "Bring " + entry.Name + " back here? It returns with its gear and settings from the last roster save. If the old " + entry.Name + " turns up later, it will be removed.";
		if (status == MHR_Roster.Status.Unsummoned)
		{
			text = "Bring " + entry.Name + " back here? It returns with its looks, skills and settings, but without its gear - that stays in the tombstone where it was unsummoned.";
		}
		else if (status == MHR_Roster.Status.Unknown)
		{
			text = text + "\n\n<color=#FFCC50>Warning:</color> " + entry.Name + " may still be alive somewhere that isn't loaded" + (entry.Dead ? " (last known: it died, and its tombstone keeps its items too)." : ".");
		}
		Push(new YesNoPopup(ModLocalization.Loc("hc_shrine_recover_title"), text, delegate
		{
			Close();
			CompanionShrine shrine = _shrine;
			if (shrine != null)
			{
				CompanionsPlugin.Log.LogInfo($"[Shrine] Recovering {entry.Name} ({status})");
				MHR_Roster.RecoverAt(entry, shrine.transform.position + shrine.transform.forward * 2f);
			}
		}, delegate
		{
			AskRecover(candidates, index + 1);
		}, localizeText: false), null, null, null);
	}

	// 1.5.78: the offering is checked here; the coins are taken when the companion is summoned
	private void Summon()
	{
		int price = CompanionTierData.Price;
		int have = CompanionManager.CoinsInPack();
		CompanionsPlugin.Log.LogInfo($"[Shrine] Summon - offering {price}, you have {have} coins");
		Close();
		if (have < price)
		{
			CompanionManager.ShowShortPopup(price, have);
			return;
		}
		StarterCompanionPanel.ShowHirePanel();
	}

	// replaces whatever of ours is showing with the next popup, then relabels its buttons
	private void Push(PopupBase popup, string right, string left, string center)
	{
		if (_active && UnifiedPopup.IsVisible())
		{
			UnifiedPopup.Pop();
		}
		UnifiedPopup.Push(popup);
		_active = true;
		UnifiedPopup up = s_popupInstance?.GetValue(null) as UnifiedPopup;
		if (up == null)
		{
			return;
		}
		SetLabel(s_rightText(up), right);
		SetLabel(s_leftText(up), left);
		SetLabel(s_centerText(up), center);
	}

	private static void SetLabel(TMP_Text t, string label)
	{
		if (t != null && label != null)
		{
			t.text = label;
		}
	}

	private void Update()
	{
		if (!_active)
		{
			return;
		}
		// closed some other way
		if (!UnifiedPopup.IsVisible())
		{
			_active = false;
			s_closedAt = Time.time;
			return;
		}
		// 1.5.79: closes like a chest - Escape, Use again (not the press that opened it), or walking away
		Player p = Player.m_localPlayer;
		bool use = Time.time - _openedAt > 0.3f && (ZInput.GetButtonDown("Use") || ZInput.GetButtonDown("JoyUse"));
		bool away = _shrine == null || p == null || p.IsDead() || Vector3.Distance(p.transform.position, _shrine.transform.position) > CloseDistance;
		if (Input.GetKeyDown(KeyCode.Escape) || ZInput.GetButtonDown("JoyButtonB") || use || away)
		{
			if (away)
			{
				CompanionsPlugin.Log.LogInfo("[Shrine] Menu closed - walked away");
			}
			Close();
		}
	}

	private void Close()
	{
		if (_active && UnifiedPopup.IsVisible())
		{
			UnifiedPopup.Pop();
		}
		_active = false;
		s_closedAt = Time.time;
	}

	private void OnDestroy()
	{
		if (_instance == this)
		{
			_instance = null;
		}
	}
}
