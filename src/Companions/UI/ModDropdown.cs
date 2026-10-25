using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Companions;

/// <summary>
/// How a list of options looks. The widget is the same code for every panel; the palette is each panel's, so the
/// extracted dropdown does not change the look of the panel it came from (the AI panel's gold-on-dark is the
/// default, and the F8 passes its own sofa of colours and a smaller row height).
/// </summary>
internal sealed class ModDropdownStyle
{
	internal TMP_FontAsset Font;

	/// <summary>Size of the text of an option (the field's text is the caller's own).</summary>
	internal float FontSize = 12f;

	/// <summary>Height of one option, in the panel's coordinates.</summary>
	internal float ItemHeight = 30f;

	internal Color ItemColor = new Color(0.16f, 0.15f, 0.13f, 0.98f);

	/// <summary>Background of the option that is in use right now.</summary>
	internal Color SelectedColor = new Color(0.83f, 0.64f, 0.31f, 1f);

	/// <summary>The band behind every option.</summary>
	internal Color Background = new Color(0.05f, 0.05f, 0.06f, 0.99f);
}

/// <summary>
/// The floating surface of the mod's hand-built dropdowns: an <b>overlay canvas</b> (its own layer, so a list is
/// neither clipped by the panel nor drawn behind a control), the <b>invisible catcher</b> that closes the open list
/// on a click anywhere else, and the lists themselves (<see cref="ModDropdownList"/>).
///
/// <para>One host per panel, created once when the panel is built. It replaces the code that used to live inside
/// <see cref="CompanionAiPanel"/> (the overlay, the catcher and one list per dropdown) so the F8 settings panel can
/// have the same widget instead of a second copy of it: the two panels never open together, which is why they may
/// share <see cref="ModUiLayers.AiPanelOverlay"/>.</para>
/// </summary>
internal sealed class ModDropdownHost
{
	private readonly List<ModDropdownList> _lists = new List<ModDropdownList>();

	/// <summary>The overlay <c>Transform</c> (a full-size child of the panel root).</summary>
	internal Transform Overlay;

	/// <summary>The invisible click catcher: active only while a list is open, so it never eats a click of the panel.</summary>
	internal GameObject Catcher;

	/// <summary>
	/// Creates the overlay and its catcher under <paramref name="panelRoot"/>. The catcher is the <b>first</b> child,
	/// so every list created after it draws (and receives the click) above it - the same shape Unity's own dropdown
	/// template uses. Nothing else is created here: the caller puts the overlay above its own controls with
	/// <c>Overlay.SetAsLastSibling()</c> when it is done building them.
	/// </summary>
	internal static ModDropdownHost Create(string name, Transform panelRoot, int sortingOrder)
	{
		GameObject overlay = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
		overlay.transform.SetParent(panelRoot, worldPositionStays: false);
		RectTransform rect = overlay.GetComponent<RectTransform>();
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
		// no Image anywhere on the overlay: it is a layer, never a surface (the catcher is the only target)
		Canvas canvas = overlay.GetComponent<Canvas>();
		canvas.overrideSorting = true;
		canvas.sortingOrder = sortingOrder;
		ModDropdownHost host = new ModDropdownHost
		{
			Overlay = overlay.transform
		};
		host.Catcher = BuildCatcher(overlay.transform, host);
		return host;
	}

	private static GameObject BuildCatcher(Transform overlay, ModDropdownHost host)
	{
		GameObject catcher = new GameObject("ListCatcher", typeof(RectTransform), typeof(Image), typeof(Button));
		catcher.transform.SetParent(overlay, worldPositionStays: false);
		RectTransform rect = catcher.GetComponent<RectTransform>();
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
		// alpha 0 is still a raycast target: it is the click outside the list, which closes it (see the summary)
		Image image = catcher.GetComponent<Image>();
		image.color = Color.clear;
		image.raycastTarget = true;
		Button button = catcher.GetComponent<Button>();
		button.transition = Selectable.Transition.None;
		button.targetGraphic = image;
		button.onClick.AddListener(host.CloseAll);
		catcher.SetActive(false);
		return catcher;
	}

	/// <summary>Registers a list, so opening one closes the others and the catcher knows when nothing is open.</summary>
	internal void Register(ModDropdownList list)
	{
		if (list != null && !_lists.Contains(list))
		{
			_lists.Add(list);
		}
	}

	/// <summary>A list is opening: the others close and the catcher goes live.</summary>
	internal void Opened(ModDropdownList list)
	{
		for (int i = 0; i < _lists.Count; i++)
		{
			if (!ReferenceEquals(_lists[i], list))
			{
				_lists[i].Hide();
			}
		}
		if (Catcher != null)
		{
			Catcher.SetActive(true);
		}
	}

	/// <summary>A list closed: the catcher is only needed while one of them is open.</summary>
	internal void Closed(ModDropdownList list)
	{
		for (int i = 0; i < _lists.Count; i++)
		{
			if (_lists[i].IsOpen)
			{
				return;
			}
		}
		if (Catcher != null)
		{
			Catcher.SetActive(false);
		}
	}

	/// <summary>Closes every list (the catcher's click, Esc, a category change, the panel hiding).</summary>
	internal void CloseAll()
	{
		for (int i = 0; i < _lists.Count; i++)
		{
			_lists[i].Hide();
		}
		if (Catcher != null)
		{
			Catcher.SetActive(false);
		}
	}

	/// <summary>Is any list open right now?</summary>
	internal bool AnyOpen
	{
		get
		{
			for (int i = 0; i < _lists.Count; i++)
			{
				if (_lists[i].IsOpen)
				{
					return true;
				}
			}
			return false;
		}
	}
}

/// <summary>
/// One list of options: a background (the border the player clicks, which must swallow the click instead of closing
/// the list), a masked viewport and a <see cref="ScrollRect"/> for the cases where the options do not fit the panel -
/// which is the whole reason this widget exists (<b>20 English voices + "follow the default"</b> do not fit under
/// the voice row of the AI panel).
///
/// <para>Geometry: everything is in the <b>panel's</b> top-left coordinates (the same ones the panel's own layout
/// uses, x to the right and y down), and the caller says how much room there is below and above the field. If the
/// list does not fit the preferred side but does fit the other one, it is opened the other way; if it fits neither,
/// it takes the side it was asked for and <b>scrolls</b> - an option is never out of reach.</para>
///
/// <para>Created once and reused: <see cref="SetOptions"/> only rebuilds the option buttons when their count changes,
/// so opening a list allocates nothing after the first time (except the strings the caller passes in, which the
/// caller already had).</para>
/// </summary>
internal sealed class ModDropdownList
{
	private sealed class Option
	{
		internal Button Button;

		internal Image Image;

		internal TMP_Text Text;
	}

	private readonly List<Option> _options = new List<Option>();

	private ModDropdownHost _host;

	private ModDropdownStyle _style;

	private RectTransform _rect;

	private RectTransform _content;

	private ScrollRect _scroll;

	private string[] _labels;

	private int _selected;

	private float _width;

	private float _height;

	/// <summary>What to do when an option is picked. The list closes itself <b>before</b> calling it.</summary>
	internal Action<int> OnPick;

	/// <summary>The list background (active only while the list is open).</summary>
	internal GameObject Root;

	internal bool IsOpen
	{
		get
		{
			return Root != null && Root.activeSelf;
		}
	}

	/// <summary>Index of the option in use (painted gold).</summary>
	internal int Selected
	{
		get
		{
			return _selected;
		}
	}

	internal static ModDropdownList Create(string name, ModDropdownHost host, ModDropdownStyle style)
	{
		if (host == null || host.Overlay == null || style == null)
		{
			return null;
		}
		GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image));
		root.transform.SetParent(host.Overlay, worldPositionStays: false);
		RectTransform rect = root.GetComponent<RectTransform>();
		Place(rect, 0f, 0f, 1f, style.ItemHeight);
		Image background = root.GetComponent<Image>();
		background.color = style.Background;
		// Deliberately a raycast target: the options are 2 px smaller than the list, so a click on that border (or on
		// the sliver under the last option) has to die in the list - otherwise it reaches the catcher and closes it.
		// It is only active while the list is open; ModUiAudit allow-lists it by name.
		background.raycastTarget = true;

		GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
		viewport.transform.SetParent(root.transform, worldPositionStays: false);
		RectTransform viewportRT = viewport.GetComponent<RectTransform>();
		viewportRT.anchorMin = Vector2.zero;
		viewportRT.anchorMax = Vector2.one;
		viewportRT.offsetMin = Vector2.zero;
		viewportRT.offsetMax = Vector2.zero;
		// the mask is what clips a long list; the viewport itself needs no Image (nothing is drawn there) and must not
		// be a raycast target (the item buttons and the list background already are)

		GameObject content = new GameObject("Content", typeof(RectTransform));
		content.transform.SetParent(viewport.transform, worldPositionStays: false);
		RectTransform contentRT = content.GetComponent<RectTransform>();
		contentRT.anchorMin = new Vector2(0f, 1f);
		contentRT.anchorMax = new Vector2(1f, 1f);
		contentRT.pivot = new Vector2(0.5f, 1f);
		contentRT.anchoredPosition = Vector2.zero;
		contentRT.sizeDelta = Vector2.zero;

		ScrollRect scroll = root.AddComponent<ScrollRect>();
		scroll.viewport = viewportRT;
		scroll.content = contentRT;
		scroll.horizontal = false;
		scroll.vertical = true;
		scroll.movementType = ScrollRect.MovementType.Clamped;
		scroll.inertia = false;
		scroll.scrollSensitivity = 20f;
		// the wheel is vertical here, which is exactly what a ScrollRect reads by itself (no helper component is
		// needed, unlike the horizontal strip of tabs of the AI panel)

		ModDropdownList list = new ModDropdownList
		{
			Root = root,
			_rect = rect,
			_content = contentRT,
			_scroll = scroll,
			_host = host,
			_style = style
		};
		host.Register(list);
		root.SetActive(false);
		return list;
	}

	/// <summary>
	/// Shows the list for <paramref name="labels"/>, with <paramref name="selected"/> painted gold. The list touches the
	/// field: it opens <b>below</b> <paramref name="fieldTop"/> + <paramref name="fieldHeight"/> (or, with
	/// <paramref name="openUp"/>, with its bottom on <paramref name="fieldTop"/>) and is as tall as the options need -
	/// capped by the room on that side. If it does not fit the preferred side but does fit the other one, it is opened
	/// the other way; if it fits neither, it scrolls (an option is never out of reach).
	/// </summary>
	/// <param name="availableBelow">Room between the bottom of the field and the bottom of the panel.</param>
	/// <param name="availableAbove">Room between the top of the panel and the top of the field.</param>
	internal void Show(string[] labels, int selected, float x, float fieldTop, float fieldHeight, float width,
		float availableBelow, float availableAbove, bool openUp, Action<int> onPick)
	{
		if (Root == null)
		{
			return;
		}
		if (onPick != null)
		{
			OnPick = onPick;
		}
		SetOptions(labels, selected);
		float total = _options.Count * _style.ItemHeight;
		float room = Mathf.Max(_style.ItemHeight, openUp ? availableAbove : availableBelow);
		float height = Mathf.Min(total, room);
		if (height < total)
		{
			// the preferred side cannot show everything: the other one may be able to, and a list that fits beats one
			// that scrolls
			float other = Mathf.Min(total, Mathf.Max(_style.ItemHeight, openUp ? availableBelow : availableAbove));
			if (other > height)
			{
				openUp = !openUp;
				height = other;
			}
		}
		_width = width;
		_height = height;
		float top = openUp ? fieldTop - height : fieldTop + fieldHeight;
		Place(_rect, x, top, width, height);
		LayoutOptions(total);
		Root.SetActive(true);
		Root.transform.SetAsLastSibling();
		if (_host != null)
		{
			_host.Opened(this);
		}
		ScrollToSelected(total);
	}

	internal void Hide()
	{
		if (Root == null || !Root.activeSelf)
		{
			if (_host != null)
			{
				_host.Closed(this);
			}
			return;
		}
		Root.SetActive(false);
		if (_host != null)
		{
			_host.Closed(this);
		}
	}

	/// <summary>
	/// Fills the list (rebuilding the option buttons only when the count changed) and paints
	/// <paramref name="selected"/>. Safe to call while the list is closed: the next <see cref="Show"/> keeps it.
	/// </summary>
	internal void SetOptions(string[] labels, int selected)
	{
		int count = labels != null ? labels.Length : 0;
		if (_options.Count != count)
		{
			RebuildOptions(count);
		}
		_labels = labels;
		for (int i = 0; i < count; i++)
		{
			Option option = _options[i];
			if (option.Text != null)
			{
				option.Text.text = labels[i];
			}
		}
		SetSelection(selected);
	}

	/// <summary>
	/// Keeps the options in the panel's current font. The panels resolve the game's font when they are built and may
	/// replace it on the first open (the game's default one is the broken Liberation - see <c>FontFix</c>), and the
	/// option buttons are created later, when the opening row's option count differs from the previous one.
	/// </summary>
	internal void SetFont(TMP_FontAsset font)
	{
		if (font != null && _style != null)
		{
			_style.Font = font;
		}
	}

	/// <summary>Paints the option in use without reopening the list.</summary>
	internal void SetSelection(int index)
	{
		_selected = index;
		for (int i = 0; i < _options.Count; i++)
		{
			Image image = _options[i].Image;
			if (image != null)
			{
				image.color = i == index ? _style.SelectedColor : _style.ItemColor;
			}
		}
	}

	/// <summary>The label at an index, or <c>"?"</c>.</summary>
	internal string LabelAt(int index)
	{
		return _labels != null && index >= 0 && index < _labels.Length ? _labels[index] : "?";
	}

	// ------------------------------------------------------------------ plumbing

	private void RebuildOptions(int count)
	{
		for (int i = 0; i < _options.Count; i++)
		{
			if (_options[i].Button != null)
			{
				UnityEngine.Object.Destroy(_options[i].Button.gameObject);
			}
		}
		_options.Clear();
		for (int i = 0; i < count; i++)
		{
			_options.Add(CreateOption(i));
		}
	}

	private Option CreateOption(int index)
	{
		GameObject option = new GameObject("Item" + index, typeof(RectTransform), typeof(Image), typeof(Button));
		option.transform.SetParent(_content, worldPositionStays: false);
		RectTransform rect = option.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0f, 1f);
		rect.anchorMax = new Vector2(0f, 1f);
		rect.pivot = new Vector2(0f, 1f);
		rect.sizeDelta = new Vector2(1f, _style.ItemHeight - 2f);
		Image image = option.GetComponent<Image>();
		image.color = _style.ItemColor;
		Button button = option.GetComponent<Button>();
		button.targetGraphic = image;
		int captured = index;
		button.onClick.AddListener(delegate
		{
			// close first: a handler that opens another list (or rebuilds the rows) must not be undone right after
			Hide();
			if (OnPick != null)
			{
				OnPick(captured);
			}
		});

		GameObject label = new GameObject("Label", typeof(RectTransform));
		label.transform.SetParent(option.transform, worldPositionStays: false);
		RectTransform labelRT = label.GetComponent<RectTransform>();
		labelRT.anchorMin = Vector2.zero;
		labelRT.anchorMax = Vector2.one;
		labelRT.offsetMin = new Vector2(2f, 2f);
		labelRT.offsetMax = new Vector2(-2f, -2f);
		TextMeshProUGUI text = label.AddComponent<TextMeshProUGUI>();
		if (_style.Font != null)
		{
			text.font = _style.Font;
		}
		text.fontSize = _style.FontSize;
		text.color = Color.white;
		text.alignment = TextAlignmentOptions.MidlineLeft;
		text.raycastTarget = false;
		text.overflowMode = TextOverflowModes.Ellipsis;
		return new Option
		{
			Button = button,
			Image = image,
			Text = text
		};
	}

	/// <summary>Places every option inside the content (which is as tall as all of them, visible or not: that is what the
	/// <see cref="ScrollRect"/> scrolls).</summary>
	private void LayoutOptions(float total)
	{
		float itemWidth = Mathf.Max(1f, _width - 4f);
		for (int i = 0; i < _options.Count; i++)
		{
			RectTransform rect = (RectTransform)_options[i].Button.transform;
			rect.sizeDelta = new Vector2(itemWidth, _style.ItemHeight - 2f);
			rect.anchoredPosition = new Vector2(2f, 0f - (i * _style.ItemHeight + 1f));
		}
		_content.sizeDelta = new Vector2(0f, total);
	}

	/// <summary>
	/// Puts the option in use inside the visible window when the list had to scroll (the voice of the companion that
	/// sits at the bottom of the 20 English voices, for instance) - the same idea the F8 uses to keep the selected row
	/// in sight.
	/// </summary>
	private void ScrollToSelected(float total)
	{
		if (_scroll == null)
		{
			return;
		}
		if (total <= _height || _selected < 0)
		{
			_scroll.verticalNormalizedPosition = 1f;
			return;
		}
		float window = total - _height;
		float itemTop = _selected * _style.ItemHeight;
		float offset = (1f - _scroll.verticalNormalizedPosition) * window;
		if (itemTop < offset)
		{
			offset = itemTop;
		}
		else if (itemTop + _style.ItemHeight > offset + _height)
		{
			offset = itemTop + _style.ItemHeight - _height;
		}
		_scroll.verticalNormalizedPosition = Mathf.Clamp01(1f - offset / window);
	}

	/// <summary>The panel's top-left coordinates of an element (x from the left edge, y from the top edge).</summary>
	internal static Vector2 TopLeftIn(RectTransform frame, RectTransform element)
	{
		if (frame == null || element == null)
		{
			return Vector2.zero;
		}
		Vector3[] corners = new Vector3[4];
		element.GetWorldCorners(corners);
		// corner 1 is the top-left one (0 = bottom-left, 1 = top-left, 2 = top-right, 3 = bottom-right)
		Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, corners[1]);
		if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(frame, screen, null, out Vector2 local))
		{
			return Vector2.zero;
		}
		Rect rect = frame.rect;
		return new Vector2(local.x - rect.xMin, rect.yMax - local.y);
	}

	/// <summary>Top-left anchored placement (the panels' own convention: y grows downwards).</summary>
	private static void Place(RectTransform rect, float x, float y, float width, float height)
	{
		rect.anchorMin = new Vector2(0f, 1f);
		rect.anchorMax = new Vector2(0f, 1f);
		rect.pivot = new Vector2(0f, 1f);
		rect.sizeDelta = new Vector2(width, height);
		rect.anchoredPosition = new Vector2(x, 0f - y);
	}
}
