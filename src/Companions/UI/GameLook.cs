using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Companions;

// 1.5.78: the mod's windows take their look from the game's own UI at runtime instead of textures packed in the mod (user:
// "I want to move away from using custom textures as much as possible ... if we ever try to make this work for consoles
// connecting to a dedicated server they wont have an issue. Also it fits in the game better."). The background is the
// one the inventory window's panels use; buttons are copies of its Craft button (CompanionPanel.CreateButton). Anything
// not found falls back to a plain colour, and the log says which.
internal static class GameLook
{
	private static Sprite s_panel;

	private static Material s_panelMat;

	private static float s_panelPpu = 1f;

	private static bool s_looked;

	// the inventory window's panel background (the player inventory panel's own image, or the largest "bkg" image in it)
	internal static Sprite PanelSprite()
	{
		if (s_looked && s_panel != null)
		{
			return s_panel;
		}
		InventoryGui gui = InventoryGui.instance;
		if (gui == null)
		{
			return null;
		}
		s_looked = true;
		Image best = null;
		float bestArea = 0f;
		string from = null;
		RectTransform[] roots = new RectTransform[3] { gui.m_player, gui.m_container, gui.m_crafting };
		foreach (RectTransform root in roots)
		{
			if (root == null)
			{
				continue;
			}
			Image own = root.GetComponent<Image>();
			if (own != null && own.sprite != null)
			{
				float area = root.rect.width * root.rect.height;
				if (area > bestArea)
				{
					best = own;
					bestArea = area;
					from = root.name;
				}
			}
			foreach (Image img in root.GetComponentsInChildren<Image>(includeInactive: true))
			{
				if (img == null || img.sprite == null || img.gameObject.name.IndexOf("bkg", StringComparison.OrdinalIgnoreCase) < 0)
				{
					continue;
				}
				RectTransform rt = img.rectTransform;
				float area = rt.rect.width * rt.rect.height;
				if (area > bestArea)
				{
					best = img;
					bestArea = area;
					from = root.name + "/" + img.gameObject.name;
				}
			}
		}
		if (best != null)
		{
			s_panel = best.sprite;
			s_panelMat = best.material;
			s_panelPpu = best.pixelsPerUnitMultiplier;
			if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogInfo($"[UI] Game look: panel background '{s_panel.name}' from the inventory window ({from}), border scale {s_panelPpu:F2}");
		}
		else
		{
			if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogInfo("[UI] Game look: no panel background found in the inventory window - plain colour");
		}
		return s_panel;
	}

	// a window or box background: the game's panel sprite, else the fallback colour
	internal static void ApplyPanel(Image img, Color fallback)
	{
		if (img == null)
		{
			return;
		}
		Sprite s = PanelSprite();
		if (s != null)
		{
			img.sprite = s;
			img.type = s.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
			// 1.5.80: the same border scale as the game's image (the edges came out stretched without it)
			img.pixelsPerUnitMultiplier = s_panelPpu;
			if (s_panelMat != null && s_panelMat != img.defaultMaterial)
			{
				img.material = s_panelMat;
			}
			img.color = Color.white;
		}
		else
		{
			img.sprite = null;
			img.color = fallback;
		}
	}

	private static bool s_scaleLogged;

	private static readonly System.Reflection.FieldInfo s_popupInstance = HarmonyLib.AccessTools.Field(typeof(UnifiedPopup), "instance");

	private static readonly System.Reflection.FieldInfo s_popupCenterText = HarmonyLib.AccessTools.Field(typeof(UnifiedPopup), "buttonCenterText");

	// 1.5.80: our canvas scales like the game's own GUI canvas (the one its popups are in), so copied sprites, borders and
	// font sizes come out the size the game draws them (user: "The summon companion page looks odd. The buttons and
	// background look like they are poorly scaled")
	internal static void MatchGameScale(CanvasScaler ours)
	{
		if (ours == null)
		{
			return;
		}
		UnifiedPopup popup = s_popupInstance?.GetValue(null) as UnifiedPopup;
		Canvas root = popup != null ? popup.GetComponentInParent<Canvas>()?.rootCanvas : null;
		CanvasScaler game = root != null ? root.GetComponent<CanvasScaler>() : null;
		if (game == null)
		{
			if (!s_scaleLogged)
			{
				s_scaleLogged = true;
				if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogInfo("[UI] Game look: no game canvas scaler found - keeping 1920x1080 scaling");
			}
			return;
		}
		ours.uiScaleMode = game.uiScaleMode;
		ours.referenceResolution = game.referenceResolution;
		ours.screenMatchMode = game.screenMatchMode;
		ours.matchWidthOrHeight = game.matchWidthOrHeight;
		ours.scaleFactor = game.scaleFactor;
		ours.referencePixelsPerUnit = game.referencePixelsPerUnit;
		if (!s_scaleLogged)
		{
			s_scaleLogged = true;
			if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogInfo($"[UI] Game look: canvas '{root.name}' {game.uiScaleMode}, reference {game.referenceResolution}, match {game.matchWidthOrHeight:F2}, scale factor {game.scaleFactor:F2} (canvas scale {root.scaleFactor:F2})");
		}
	}

	// 1.5.80: button labels like the game popup's OK button (user: "It should be something close to how the 'ok' button
	// looks when the message about being short gold coins pops up")
	internal static void ApplyButtonText(TMP_Text t)
	{
		UnifiedPopup popup = s_popupInstance?.GetValue(null) as UnifiedPopup;
		TMP_Text src = popup != null ? s_popupCenterText?.GetValue(popup) as TMP_Text : null;
		if (t == null || src == null)
		{
			return;
		}
		if (src.font != null)
		{
			t.font = src.font;
			t.fontSharedMaterial = src.fontSharedMaterial;
		}
		float size = src.enableAutoSizing ? src.fontSizeMax : src.fontSize;
		t.enableAutoSizing = true;
		t.fontSizeMax = size;
		t.fontSizeMin = Mathf.Min(10f, size);
		t.fontSize = size;
		t.fontStyle = src.fontStyle;
	}

	// the game's Craft button, to copy
	internal static GameObject ButtonTemplate()
	{
		return InventoryGui.instance != null && InventoryGui.instance.m_craftButton != null ? InventoryGui.instance.m_craftButton.gameObject : null;
	}

	/// <summary>
	/// The invisible vertical bar of a <see cref="ScrollRect"/> of the mod. Extracted (04/11/2026, the build panel) from
	/// the two copies the F8 panel and the companion window used to carry: the bar and its handle are
	/// <b>not</b> raycast targets and the bar is the <b>first</b> sibling of the viewport, so the topmost graphic of a
	/// list is a control and never a decorative element (that was one of the suspects of the swallowed click of the
	/// UI audit). The <see cref="ScrollRect"/> holds it by reference, so neither the wheel nor the drag depends on it
	/// being drawn - the scroll area (the viewport image) and the rows receive those.
	/// </summary>
	internal static Scrollbar CreateHiddenScrollbar(Transform parent)
	{
		GameObject bar = new GameObject("Scrollbar", typeof(RectTransform));
		bar.transform.SetParent(parent, worldPositionStays: false);
		RectTransform barRT = bar.GetComponent<RectTransform>();
		barRT.anchorMin = new Vector2(1f, 0f);
		barRT.anchorMax = new Vector2(1f, 1f);
		barRT.pivot = new Vector2(1f, 0.5f);
		barRT.sizeDelta = new Vector2(10f, 0f);
		Image barImage = bar.AddComponent<Image>();
		barImage.color = Color.clear;
		barImage.raycastTarget = false;
		GameObject area = new GameObject("Sliding Area", typeof(RectTransform));
		area.transform.SetParent(bar.transform, worldPositionStays: false);
		RectTransform areaRT = area.GetComponent<RectTransform>();
		areaRT.anchorMin = Vector2.zero;
		areaRT.anchorMax = Vector2.one;
		areaRT.offsetMin = Vector2.zero;
		areaRT.offsetMax = Vector2.zero;
		GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
		handle.transform.SetParent(area.transform, worldPositionStays: false);
		Image handleImage = handle.GetComponent<Image>();
		handleImage.color = Color.clear;
		handleImage.raycastTarget = false;
		RectTransform handleRT = handle.GetComponent<RectTransform>();
		handleRT.anchorMin = Vector2.zero;
		handleRT.anchorMax = new Vector2(1f, 0.5f);
		handleRT.offsetMin = Vector2.zero;
		handleRT.offsetMax = Vector2.zero;
		Scrollbar scrollbar = bar.AddComponent<Scrollbar>();
		scrollbar.handleRect = handleRT;
		scrollbar.direction = Scrollbar.Direction.BottomToTop;
		// first sibling of the viewport: the invisible bar sits BELOW the content instead of on top of it
		bar.transform.SetAsFirstSibling();
		return scrollbar;
	}
}
