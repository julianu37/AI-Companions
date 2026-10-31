using System;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;

namespace Companions;

// 1.5.72: the "press a button" control for a controller bind in the F8 panel (Controller section). Started with the
// controller's A on the row or a click; waits for every button to be let go, then takes the next one pressed. On a
// modifier row the block button is stored as "Alt" (the game's own modifier, follows the layout). Esc or 6 s cancels.
internal class PadBindListener : MonoBehaviour
{
	private ConfigEntry<string> _entry;

	private TMP_Text _label;

	private bool _listening;

	private bool _released;

	private float _startedAt;

	private int _startFrame = -1;

	private RectTransform _rect;

	internal void Init(ConfigEntry<string> entry, TMP_Text label)
	{
		_entry = entry;
		_label = label;
		_rect = GetComponent<RectTransform>();
	}

	internal static string Label(string v)
	{
		return (v == null || v == "None") ? "None" : (v == "Alt" ? "Alt (" + MHR_Pad.Glyph("Alt") + ")" : MHR_Pad.Glyph(v));
	}

	internal void Begin()
	{
		if (_listening)
		{
			return;
		}
		_listening = true;
		_released = false;
		_startedAt = Time.unscaledTime;
		_startFrame = Time.frameCount;
		KeyBindListener.Capturing = true;
		_label.text = "Press a button...";
		if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Config] Waiting for a controller button for " + _entry.Definition.Key + " (Esc cancels)");
	}

	private void End(string value)
	{
		_listening = false;
		KeyBindListener.Capturing = false;
		KeyBindListener.CaptureEndFrame = Time.frameCount;
		if (value != null)
		{
			_entry.Value = value;
			if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Config] " + _entry.Definition.Key + " = " + value);
		}
		_label.text = Label(_entry.Value);
	}

	private void Update()
	{
		if (!_listening)
		{
			if (Input.GetMouseButtonDown(0) && _rect != null && RectTransformUtility.RectangleContainsScreenPoint(_rect, Input.mousePosition, null))
			{
				Begin();
			}
			return;
		}
		if (Time.frameCount == _startFrame)
		{
			return;
		}
		if (Input.GetKeyDown(KeyCode.Escape) || Time.unscaledTime - _startedAt > 6f)
		{
			End(null);
			return;
		}
		bool anyHeld = false;
		foreach (string b in MHR_Pad.Physical)
		{
			if (ZInput.GetButton(MHR_Pad.Joy(b)))
			{
				anyHeld = true;
				if (_released && ZInput.GetButtonDown(MHR_Pad.Joy(b)))
				{
					string v = b;
					if (_entry.Definition.Key.EndsWith("Modifier") && MHR_Pad.PathOf(MHR_Pad.Joy(b)) == MHR_Pad.PathOf("JoyAltKeys"))
					{
						v = "Alt";
					}
					End(v);
					return;
				}
			}
		}
		if (!anyHeld)
		{
			_released = true;
		}
	}
}
