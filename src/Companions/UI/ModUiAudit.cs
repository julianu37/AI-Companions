using System;
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
/// <para><b>It is opt-in</b>: it only runs with <c>[Advanced] UiDiag = true</c> (off by default, see
/// <see cref="Enabled"/>). With the audit off the callers return before entering this class, so <b>not a single line
/// is written and nothing is walked</b> - not the graphics of the panel, not the canvases of the scene. It became a
/// switch because one session of it wrote <b>~7 KB in six lines</b> (most of it the list of the scene's canvases,
/// which is the one part that is worth keeping) and three of those lines were <b>false positives</b> of the AI panel:
/// targets that are there on purpose, now named in <see cref="Intentional"/>. Turn it on when a click or a piece of
/// UI looks wrong - the log then says who is in front - and expect a bigger log. It changes nothing in the game.</para>
///
/// <para>It reports
/// <list type="bullet">
/// <item>every raycast target of the panel that is <b>not</b> a control (a <c>Selectable</c> in its parents): there
/// should be none, and the documented exceptions - the modal backdrop, the scroll areas and the backgrounds of the
/// open dropdown lists (<see cref="Intentional"/>) - are only <b>counted</b>, never listed, because a name list of
/// every decorative target was most of the old log for nothing;</item>
/// <item>every other <b>active</b> canvas of the scene with its sorting order - the layer data - and, for the
/// non-mod ones at this layer or above (a lower one can never win), any of its raycast targets whose screen rect
/// covers the probed column of controls. A hit there <b>is</b> the invisible band: it is the part that pays off, so
/// it stays line by line, with the path, the type, the size and the probed point.</item>
/// </list>
/// </para>
/// </summary>
internal static class ModUiAudit
{
	/// <summary>
	/// <c>[Advanced] UiDiag</c>: <c>false</c> by default, and while it is <c>false</c> this class does nothing at all.
	/// </summary>
	internal static bool Enabled
	{
		get
		{
			return ModConfig.UiDiag != null && ModConfig.UiDiag.Value;
		}
	}

	/// <summary>True once the two lines that explain the audit - that it is on, and the allow-list it will never
	/// report - have been written: they are the lines that explain why the log grew.</summary>
	private static bool _announced;

	/// <summary>
	/// Logs the audit of one panel. Does nothing (and touches nothing) while <see cref="Enabled"/> is <c>false</c>.
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
		if (!Enabled || owner == null || panelRoot == null || probePoints == null || probePoints.Length == 0)
		{
			return;
		}
		try
		{
			if (!_announced)
			{
				_announced = true;
				CompanionsPlugin.Log?.LogInfo("[UiDiag] Raycast audit enabled ([Advanced] UiDiag = true): nothing in the"
					+ " game changes, this only writes to the log, once per panel - and the log grows.");
				// The allow-list, said out loud once: it is what the audit will NOT report, and why. Without it the
				// reader of the log has no way to tell a target that was checked and accepted from one that was
				// never seen.
				string allowed = "";
				for (int i = 0; i < Intentional.Length; i++)
				{
					allowed += ((i > 0) ? "; " : "") + Intentional[i].Tail + " (" + Intentional[i].Reason + ")";
				}
				CompanionsPlugin.Log?.LogInfo("[UiDiag] Raycast targets that are on purpose (never reported): " + allowed + ".");
			}
			// 1. the panel itself: every raycast target that is not a control
			Graphic[] graphics = owner.gameObject.GetComponentsInChildren<Graphic>(includeInactive: true);
			int controls = 0;
			int onPurpose = 0;
			int unexpected = 0;
			for (int i = 0; i < graphics.Length; i++)
			{
				Graphic graphic = graphics[i];
				if (graphic == null || !graphic.raycastTarget)
				{
					continue;
				}
				string path = Path(graphic.transform);
				string reason = NonControlRaycastReason(graphic, backdrop, owner, path);
				if (reason == null)
				{
					controls++;
				}
				else if (reason.Length == 0)
				{
					onPurpose++;
				}
				else
				{
					unexpected++;
					CompanionsPlugin.Log?.LogWarning("[UiDiag] " + label
						+ ": UNEXPECTED raycast target inside the panel: " + path + " (" + graphic.GetType().Name
						+ ", " + reason + ")");
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
			// One line per panel: the counts, never the names of the decorative targets (that list was the bulk of
			// the log). The layer list is the one thing that has to stay: it is what named the game's HUD at 400.
			CompanionsPlugin.Log?.LogInfo("[UiDiag] " + label + ": panel order=" + panelOrder + ", "
				+ (controls + onPurpose + unexpected) + " raycast target(s) (" + controls + " control(s), "
				+ onPurpose + " on purpose), " + unexpected + " unexpected; " + canvases + " other active canvas(es): "
				+ ((others.Length > 0) ? others : "none") + "; " + over + " of them over the controls.");
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
	/// The allow-list of the audit: the raycast targets that are <b>there on purpose</b> even though no
	/// <c>Selectable</c> is in their parents - matched by the <b>tail</b> of the path, so the name of the panel that
	/// owns them is not part of the contract - each one with the reason it has to stay a target. Everything else that
	/// is not a control is reported as unexpected. (The modal backdrop and the scroll areas of a panel are not in this
	/// table: they are recognized by structure - see <see cref="NonControlRaycastReason"/> - because every panel has
	/// them and the panel hands its own backdrop over.)
	/// </summary>
	private static readonly IntentionalTarget[] Intentional =
	{
		new IntentionalTarget("Overlay/TierList",
			"the background of an open dropdown list: its options are 2 px smaller than the list, so the click on that "
			+ "border would fall through to the list catcher (which closes the list) or to a control behind it - the "
			+ "list must swallow it, and it is only active while the list is open"),
		new IntentionalTarget("Overlay/CompPersonalityList",
			"the background of an open dropdown list, same as Overlay/TierList (Fase 3.10: the personality of a "
			+ "companion, on its own tab)"),
		new IntentionalTarget("Overlay/CompVoiceList",
			"the background of an open dropdown list, same as Overlay/TierList (Fase 3.10: the voice of a "
			+ "companion, on its own tab)")
	};

	/// <summary>
	/// Classifies a raycast target of the panel: <c>null</c> for a control (a <c>Selectable</c> in its parents), an
	/// empty string for a documented exception (the modal backdrop, the scroll areas - the GameObjects the
	/// <c>ScrollRect</c>s live on, where the wheel and the drag are received - and the entries of
	/// <see cref="Intentional"/>), otherwise a short reason why it is a suspect.
	/// </summary>
	private static string NonControlRaycastReason(Graphic graphic, GameObject backdrop, Component owner, string path)
	{
		if (graphic.gameObject == backdrop)
		{
			return string.Empty;
		}
		for (int i = 0; i < Intentional.Length; i++)
		{
			IntentionalTarget entry = Intentional[i];
			if (path.Length >= entry.Tail.Length
				&& string.CompareOrdinal(path, path.Length - entry.Tail.Length, entry.Tail, 0, entry.Tail.Length) == 0)
			{
				return string.Empty;
			}
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

	/// <summary>One entry of the allow-list: the tail of the path and why that target is intentional.</summary>
	private sealed class IntentionalTarget
	{
		internal readonly string Tail;

		internal readonly string Reason;

		internal IntentionalTarget(string tail, string reason)
		{
			Tail = tail;
			Reason = reason;
		}
	}
}
