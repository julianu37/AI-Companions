using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Companions;

/// <summary>
/// The one-shot raycast audit of a mod panel: the answer to "who is in front of the controls?" - the only question
/// that matters when a click is swallowed again.
///
/// <para>It was born inside <see cref="ConfigPanel"/> to find the invisible band that ate the F8 clicks (it found it:
/// the game's <c>HUD</c> canvas at <b>400</b>, with its <c>crosshair/HoverName</c> target in the centre of the
/// screen), and it is shared now because the layer numbers are: it must report whoever ends up <b>above</b> a panel,
/// which is a property of <see cref="ModUiLayers"/> and not of one window. Both full screen panels call it
/// <b>once per session</b>, on their first open - never per frame - and it never throws into <c>Show()</c>.</para>
///
/// <para>It reports
/// <list type="bullet">
/// <item>every raycast target of the panel that is <b>not</b> a control (a <c>Selectable</c> in its parents): there
/// should be none, the modal backdrop and the scroll areas are the only documented exceptions;</item>
/// <item>every other <b>active</b> canvas of the scene with its sorting order - the layer data - and, for the
/// non-mod ones at this layer or above (a lower one can never win), any of its raycast targets whose screen rect
/// covers the probed column of controls. A hit there <b>is</b> the invisible band.</item>
/// </list>
/// </para>
/// </summary>
internal static class ModUiAudit
{
	/// <summary>
	/// Logs the audit of one panel.
	/// </summary>
	/// <param name="label">Name of the audit in every line, e.g. <c>"F8 raycast audit"</c>.</param>
	/// <param name="panelOrder">The sorting order of the panel's own canvas (<see cref="ModUiLayers"/>).</param>
	/// <param name="ownCanvas">The panel's own canvas: it is the one doing the asking, so it is not reported.</param>
	/// <param name="panelRoot">The panel's root <c>RectTransform</c>, the frame of the probe points.</param>
	/// <param name="probePoints">Panel-local points probed in every canvas above the panel (the column of controls).</param>
	/// <param name="backdrop">The modal backdrop of the panel: the one raycast target that is there on purpose.</param>
	/// <param name="owner">The panel component, on the GameObject that carries the canvas.</param>
	internal static void Log(string label, int panelOrder, Canvas ownCanvas, RectTransform panelRoot,
		Vector2[] probePoints, GameObject backdrop, Component owner)
	{
		if (owner == null || panelRoot == null || probePoints == null || probePoints.Length == 0)
		{
			return;
		}
		try
		{
			// 1. the panel itself: every raycast target that is not a control
			Graphic[] graphics = owner.gameObject.GetComponentsInChildren<Graphic>(includeInactive: true);
			int controls = 0;
			int unexpected = 0;
			List<string> onPurpose = new List<string>();
			for (int i = 0; i < graphics.Length; i++)
			{
				Graphic graphic = graphics[i];
				if (graphic == null || !graphic.raycastTarget)
				{
					continue;
				}
				string reason = NonControlRaycastReason(graphic, backdrop, owner);
				if (reason == null)
				{
					controls++;
				}
				else if (reason.Length == 0)
				{
					onPurpose.Add(Path(graphic.transform));
				}
				else
				{
					unexpected++;
					CompanionsPlugin.Log?.LogWarning("[UiDiag] " + label
						+ ": UNEXPECTED raycast target inside the panel: " + Path(graphic.transform) + " (" + reason + ")");
				}
			}
			// 2. the rest of the scene: the layers, and what sits over the controls of this panel
			string others = "";
			int canvases = 0;
			int over = 0;
			Canvas[] all = Resources.FindObjectsOfTypeAll<Canvas>();
			for (int i = 0; i < all.Length; i++)
			{
				Canvas canvas = all[i];
				if (canvas == null || canvas == ownCanvas || !canvas.gameObject.scene.IsValid() || !canvas.isActiveAndEnabled)
				{
					continue;
				}
				canvases++;
				if (others.Length > 0)
				{
					others += ", ";
				}
				others += canvas.name + "=" + canvas.sortingOrder + (canvas.overrideSorting ? "(override)" : "");
				// The mod's own canvases are listed (they are part of the picture) but never probed: the mod never
				// leaves two of its own windows on top of each other - every one of them refuses to open over the
				// others - so a hit there would only be the audit finding itself.
				if (canvas.sortingOrder >= panelOrder && !IsModCanvas(canvas))
				{
					over += LogCanvasOverPanel(label, canvas, panelRoot, probePoints);
				}
			}
			CompanionsPlugin.Log?.LogInfo("[UiDiag] " + label + ": panel order=" + panelOrder + ", "
				+ controls + " control raycast target(s), " + onPurpose.Count + " on purpose ("
				+ string.Join(", ", onPurpose.ToArray()) + "), " + unexpected + " unexpected"
				+ "; " + canvases + " other active canvas(es): " + others
				+ "; " + over + " raycast target(s) of another canvas over the controls.");
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[UiDiag] " + label + " failed: " + ex.Message);
		}
	}

	/// <summary>
	/// <c>true</c> for a canvas that belongs to this mod (one of the panels, or the overlay of one of them). Walked up
	/// from the canvas: the panel component always sits on the GameObject that carries the canvas, or above it.
	/// </summary>
	private static bool IsModCanvas(Canvas canvas)
	{
		for (Transform walk = canvas.transform; walk != null; walk = walk.parent)
		{
			if (walk.GetComponent<ConfigPanel>() != null
				|| walk.GetComponent<CompanionAiPanel>() != null
				|| walk.GetComponent<CompanionRadialMenu>() != null
				|| walk.GetComponent<HomeZonePanel>() != null
				|| walk.GetComponent<StarterCompanionPanel>() != null
				|| walk.GetComponent<FarmZonePlacer>() != null
				|| walk.GetComponent<FarmZoneCropPicker>() != null)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Classifies a raycast target of the panel: <c>null</c> for a control (a <c>Selectable</c> in its parents), an
	/// empty string for a documented exception (the modal backdrop and the scroll areas, the GameObjects the
	/// <c>ScrollRect</c>s live on - the wheel and the drag are received there), otherwise a short reason why it is a
	/// suspect.
	/// </summary>
	private static string NonControlRaycastReason(Graphic graphic, GameObject backdrop, Component owner)
	{
		if (graphic.gameObject == backdrop)
		{
			return string.Empty;
		}
		bool selectable = false;
		bool scrollbar = false;
		for (Transform walk = graphic.transform; walk != null; walk = walk.parent)
		{
			if (walk.GetComponent<ScrollRect>() != null)
			{
				return string.Empty;
			}
			if (walk.GetComponent<Scrollbar>() != null)
			{
				scrollbar = true;
			}
			if (walk.GetComponent<Selectable>() != null)
			{
				selectable = true;
			}
			if (walk.gameObject == owner.gameObject)
			{
				break;
			}
		}
		if (scrollbar)
		{
			return "inside a Scrollbar (an invisible bar must not be a raycast target)";
		}
		if (selectable)
		{
			return null;
		}
		return "no Selectable in its parents (decorative)";
	}

	/// <summary>
	/// Logs every raycast target of <paramref name="canvas"/> that covers the probed column of controls of the panel
	/// and returns how many there were. Only canvases at the panel's sorting order or above reach this point: a lower
	/// one can never be in front of the panel.
	/// </summary>
	private static int LogCanvasOverPanel(string label, Canvas canvas, RectTransform panelRoot, Vector2[] probePoints)
	{
		Camera camera = ((canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera);
		Graphic[] graphics = canvas.GetComponentsInChildren<Graphic>(includeInactive: true);
		int hits = 0;
		for (int i = 0; i < graphics.Length; i++)
		{
			Graphic graphic = graphics[i];
			if (graphic == null || !graphic.raycastTarget || !graphic.isActiveAndEnabled)
			{
				continue;
			}
			for (int p = 0; p < probePoints.Length; p++)
			{
				if (!RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform, ProbeScreenPoint(panelRoot, probePoints[p]), camera))
				{
					continue;
				}
				hits++;
				Rect rect = graphic.rectTransform.rect;
				CompanionsPlugin.Log?.LogWarning("[UiDiag] " + label + ": canvas \"" + canvas.name + "\" (order="
					+ canvas.sortingOrder + (canvas.overrideSorting ? ", overrideSorting" : "")
					+ ") has a raycast target over the controls of the panel: " + Path(graphic.transform) + " ("
					+ graphic.GetType().Name + " " + Mathf.RoundToInt(rect.width) + "x" + Mathf.RoundToInt(rect.height)
					+ ") at the panel point " + probePoints[p].x + "," + probePoints[p].y);
				break;
			}
		}
		return hits;
	}

	/// <summary>Screen point of a panel-local point. The panel's canvas is Screen Space Overlay, so the world position
	/// of a UI element already is its screen position and no camera is needed.</summary>
	private static Vector2 ProbeScreenPoint(RectTransform panelRoot, Vector2 panelLocal)
	{
		return RectTransformUtility.WorldToScreenPoint(null, panelRoot.TransformPoint(panelLocal));
	}

	private static string Path(Transform node)
	{
		string text = node.name;
		for (Transform parent = node.parent; parent != null; parent = parent.parent)
		{
			text = parent.name + "/" + text;
		}
		return text;
	}
}
