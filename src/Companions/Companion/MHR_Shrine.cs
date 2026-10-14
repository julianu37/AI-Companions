using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace Companions;

// 1.5.1: Odin's Shrine - a buildable stone that shows your companion roster and brings back a lost companion, using the
// game's own message windows. Built with the hammer at a workbench ([Death] ShrineRecipe). The model is a copy of a
// vanilla one ([Death] ShrineModel): the player tombstone (default) or the ward stone, with their own behaviour removed.
internal static class MHR_Shrine
{
	internal const string PrefabName = "HC_CompanionShrine";

	internal const string DisplayName = "Odin's Shrine";

	// The piece's name and description are set once (and again on every world load). The game localizes Piece.m_name /
	// m_description as-is, so the localized text is written in directly, with the English name as the fallback for the
	// (unlikely) case of the language dictionary not being up yet.
	private static string LocalizedName()
	{
		if (Localization.instance == null)
		{
			return DisplayName;
		}
		return ModLocalization.Loc("hc_shrine_name");
	}

	// the hover name of the shrine (CompanionShrine.GetHoverName)
	internal static string HoverName()
	{
		return LocalizedName();
	}

	private static string LocalizedDescription()
	{
		if (Localization.instance == null)
		{
			return "Shows your companions and whether they're alive, and brings back one that's been lost.";
		}
		return ModLocalization.Loc("hc_shrine_desc");
	}

	private static GameObject s_holder;

	private static GameObject s_prefab;

	private static string s_model;

	// called on every ZNetScene.Awake (each world load). The piece is always registered - a world with shrines in it
	// would otherwise lose them while ShrineBuildable is off - and only left out of the hammer when it's off.
	internal static void Init(ZNetScene zs)
	{
		try
		{
			string text = ModelPrefabName();
			if (s_prefab != null && s_model != text)
			{
				UnityEngine.Object.Destroy(s_prefab);
				s_prefab = null;
			}
			if (s_prefab == null && !Create(zs, text))
			{
				return;
			}
			if (zs.GetPrefab(PrefabName) == null)
			{
				CompanionPrefabs.RegisterPrefab(s_prefab, zs);
			}
			Piece component = s_prefab.GetComponent<Piece>();
			component.m_resources = ParseRecipe(zs, ModConfig.ShrineRecipe.Value);
			// the name and description are localized text; they are refreshed on every world load so the piece always
			// matches the language in use
			component.m_name = LocalizedName();
			component.m_description = LocalizedDescription();
			component.m_craftingStation = zs.GetPrefab("piece_workbench")?.GetComponent<CraftingStation>();
			PieceTable pieceTable = zs.GetPrefab("Hammer")?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_buildPieces;
			if (pieceTable == null)
			{
				CompanionsPlugin.Log.LogWarning("[Shrine] Hammer build table not found - Odin's Shrine can't be built");
				return;
			}
			pieceTable.m_pieces.RemoveAll((GameObject p) => p == null || p.name == PrefabName);
			if (ModConfig.ShrineBuildable.Value)
			{
				pieceTable.m_pieces.Add(s_prefab);
				CompanionsPlugin.Log.LogInfo("[Shrine] Odin's Shrine added to the hammer (Misc), recipe " + ModConfig.ShrineRecipe.Value + " at a workbench");
			}
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log.LogWarning("[Shrine] Setup failed: " + ex);
		}
	}

	private static string ModelPrefabName()
	{
		string text = (ModConfig.ShrineModel.Value ?? "").Trim();
		if (!text.Equals("Ward", StringComparison.OrdinalIgnoreCase))
		{
			return "Player_tombstone";
		}
		return "guard_stone";
	}

	private static bool Create(ZNetScene zs, string model)
	{
		GameObject prefab = zs.GetPrefab(model);
		if (prefab == null)
		{
			CompanionsPlugin.Log.LogWarning("[Shrine] Model " + model + " not found - no Odin's Shrine");
			return false;
		}
		if (s_holder == null)
		{
			s_holder = new GameObject("HC_ShrinePrefab");
			UnityEngine.Object.DontDestroyOnLoad(s_holder);
			s_holder.SetActive(value: false);
		}
		GameObject gameObject = UnityEngine.Object.Instantiate(prefab, s_holder.transform, worldPositionStays: false);
		gameObject.name = PrefabName;
		float hoverOffset = 1f;
		Piece piece = gameObject.GetComponent<Piece>();
		TombStone component = gameObject.GetComponent<TombStone>();
		if (component != null)
		{
			hoverOffset = component.m_hoverOffset;
			if (component.m_worldText != null)
			{
				component.m_worldText.text = LocalizedName();
			}
			if (component.m_floater != null && component.m_floater != gameObject)
			{
				component.m_floater.SetActive(value: false);
			}
			// a player tombstone is a loose physics object with a chest in it - the shrine is a fixed building piece
			Remove<TombStone>(gameObject);
			Remove<Floating>(gameObject);
			Remove<Container>(gameObject);
			Remove<ZSyncTransform>(gameObject);
			Remove<Rigidbody>(gameObject);
		}
		PrivateArea component2 = gameObject.GetComponent<PrivateArea>();
		if (component2 != null)
		{
			hoverOffset = component2.m_hoverOffset;
			if (component2.m_areaMarker != null)
			{
				UnityEngine.Object.DestroyImmediate(component2.m_areaMarker.gameObject);
			}
			if (component2.m_inRangeEffect != null && component2.m_inRangeEffect.transform.IsChildOf(gameObject.transform))
			{
				component2.m_inRangeEffect.SetActive(value: false);
			}
			if (component2.m_enabledEffect != null && component2.m_enabledEffect.transform.IsChildOf(gameObject.transform))
			{
				component2.m_enabledEffect.SetActive(value: true);
			}
			UnityEngine.Object.DestroyImmediate(component2);
		}
		CircleProjector[] componentsInChildren = gameObject.GetComponentsInChildren<CircleProjector>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			UnityEngine.Object.DestroyImmediate(componentsInChildren[i]);
		}
		Piece piece2 = zs.GetPrefab("guard_stone")?.GetComponent<Piece>();
		if (piece == null)
		{
			piece = gameObject.AddComponent<Piece>();
			if (piece2 != null)
			{
				piece.m_placeEffect = piece2.m_placeEffect;
			}
			piece.m_icon = zs.GetPrefab("TrophySkeleton")?.GetComponent<ItemDrop>()?.m_itemData?.GetIcon() ?? piece2?.m_icon;
		}
		// the hammer only removes (and you only aim at) things on the building layers
		int num = LayerMask.NameToLayer("piece");
		Collider[] componentsInChildren2 = gameObject.GetComponentsInChildren<Collider>(includeInactive: true);
		foreach (Collider collider in componentsInChildren2)
		{
			if (num >= 0 && !collider.isTrigger)
			{
				collider.gameObject.layer = num;
			}
		}
		piece.m_name = LocalizedName();
		piece.m_description = LocalizedDescription();
		piece.m_category = Piece.PieceCategory.Misc;
		piece.m_enabled = true;
		gameObject.AddComponent<CompanionShrine>().m_hoverOffset = hoverOffset;
		s_prefab = gameObject;
		s_model = model;
		CompanionsPlugin.Log.LogInfo("[Shrine] Odin's Shrine piece created (model: " + model + ")");
		return true;
	}

	private static void Remove<T>(GameObject go) where T : Component
	{
		T[] components = go.GetComponents<T>();
		foreach (T val in components)
		{
			try
			{
				UnityEngine.Object.DestroyImmediate(val);
			}
			catch (Exception ex)
			{
				CompanionsPlugin.Log.LogWarning("[Shrine] Couldn't remove " + typeof(T).Name + ": " + ex.Message);
			}
		}
	}

	// "Stone:10,Wood:5,GreydwarfEye:3"
	private static Piece.Requirement[] ParseRecipe(ZNetScene zs, string recipe)
	{
		List<Piece.Requirement> list = new List<Piece.Requirement>();
		string[] array = (recipe ?? "").Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
		foreach (string text in array)
		{
			string[] array2 = text.Split(new char[1] { ':' });
			string text2 = array2[0].Trim();
			int result = 1;
			if (array2.Length > 1 && !int.TryParse(array2[1].Trim(), out result))
			{
				result = 1;
			}
			ItemDrop itemDrop = zs.GetPrefab(text2)?.GetComponent<ItemDrop>();
			if (itemDrop == null)
			{
				CompanionsPlugin.Log.LogWarning("[Shrine] ShrineRecipe: unknown item \"" + text2 + "\" - skipped");
			}
			else if (result > 0)
			{
				list.Add(new Piece.Requirement
				{
					m_resItem = itemDrop,
					m_amount = result,
					m_recover = true
				});
			}
		}
		return list.ToArray();
	}

	private static string StatusText(MHR_Roster.Status s)
	{
		return s switch
		{
			MHR_Roster.Status.Alive => "<color=#80E080>" + ModLocalization.Loc("hc_roster_alive") + "</color>",
			MHR_Roster.Status.Dead => "<color=#C0C0C0>" + ModLocalization.Loc("hc_roster_dead") + "</color>" + ModLocalization.Loc("hc_roster_dead_note"),
			MHR_Roster.Status.Lost => "<color=#FF7060>" + ModLocalization.Loc("hc_roster_lost") + "</color>",
			_ => "<color=#FFCC50>" + ModLocalization.Loc("hc_roster_unknown") + "</color>" + ModLocalization.Loc("hc_roster_unknown_note"),
		};
	}

	// Using the shrine: the roster in the game's message window, then a Yes / No for each companion that can be brought
	// back (Lost first, then Unknown). No moves on to the next one.
	internal static void Use(CompanionShrine shrine)
	{
		if (!UnifiedPopup.IsAvailable() || UnifiedPopup.IsVisible())
		{
			return;
		}
		List<KeyValuePair<MHR_Roster.Entry, MHR_Roster.Status>> roster = MHR_Roster.GetRoster();
		CompanionsPlugin.Log.LogInfo($"[Shrine] Used - {roster.Count} companion(s) on the roster");
		StringBuilder stringBuilder = new StringBuilder();
		List<KeyValuePair<MHR_Roster.Entry, MHR_Roster.Status>> list = new List<KeyValuePair<MHR_Roster.Entry, MHR_Roster.Status>>();
		foreach (KeyValuePair<MHR_Roster.Entry, MHR_Roster.Status> item in roster)
		{
			stringBuilder.Append(item.Key.Name).Append(": ").Append(StatusText(item.Value))
				.Append('\n');
			if (item.Value == MHR_Roster.Status.Lost)
			{
				list.Add(item);
			}
		}
		foreach (KeyValuePair<MHR_Roster.Entry, MHR_Roster.Status> item2 in roster)
		{
			if (item2.Value == MHR_Roster.Status.Unknown)
			{
				list.Add(item2);
			}
		}
		string text = stringBuilder.ToString().TrimEnd('\n');
		if (roster.Count == 0)
		{
			text = ModLocalization.Loc("hc_shrine_empty");
		}
		if (list.Count == 0)
		{
			UnifiedPopup.Push(new WarningPopup(LocalizedName(), text + ((roster.Count > 0) ? "\n\n" + ModLocalization.Loc("hc_shrine_none_to_recover") : ""), delegate
			{
				UnifiedPopup.Pop();
			}, localizeText: false));
		}
		else
		{
			Ask(shrine, text, list, 0);
		}
	}

	private static void Ask(CompanionShrine shrine, string rosterText, List<KeyValuePair<MHR_Roster.Entry, MHR_Roster.Status>> candidates, int index)
	{
		if (index >= candidates.Count || shrine == null)
		{
			return;
		}
		MHR_Roster.Entry entry = candidates[index].Key;
		string text = rosterText + "\n\n" + ModLocalization.LocFmt("hc_shrine_bring_back", entry.Name);
		if (candidates[index].Value == MHR_Roster.Status.Unknown)
		{
			text = text + "\n\n<color=#FFCC50>" + ModLocalization.Loc("hc_shrine_warning") + "</color> " + ModLocalization.LocFmt(entry.Dead ? "hc_shrine_unknown_dead" : "hc_shrine_unknown_alive", entry.Name);
		}
		UnifiedPopup.Push(new YesNoPopup(LocalizedName(), text, delegate
		{
			UnifiedPopup.Pop();
			if (shrine != null)
			{
				MHR_Roster.RecoverAt(entry, shrine.transform.position + shrine.transform.forward * 2f);
			}
		}, delegate
		{
			UnifiedPopup.Pop();
			Ask(shrine, rosterText, candidates, index + 1);
		}, localizeText: false));
	}
}

public class CompanionShrine : MonoBehaviour, Hoverable, Interactable
{
	internal float m_hoverOffset = 1f;

	public string GetHoverText()
	{
		return Localization.instance.Localize(ModLocalization.Loc("hc_shrine_name") + "\n[<color=yellow><b>$KEY_Use</b></color>] " + ModLocalization.Loc("hc_shrine_hover"));
	}

	public string GetHoverName()
	{
		return MHR_Shrine.HoverName();
	}

	public float GetHoverOffset()
	{
		return m_hoverOffset;
	}

	public bool Interact(Humanoid user, bool hold, bool alt)
	{
		if (hold || user == null || user != Player.m_localPlayer)
		{
			return false;
		}
		MHR_Shrine.Use(this);
		return true;
	}

	public bool UseItem(Humanoid user, ItemDrop.ItemData item)
	{
		return false;
	}
}
