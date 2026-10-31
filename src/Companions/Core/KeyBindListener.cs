using System;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;

namespace Companions;

// The "press a key" button in the F8 panel. 1.5.44: the user couldn't bind the new stance keys - clicking the button did
// nothing (no "Press a key..."), and the F1 Configuration Manager didn't take a key either. So it no longer relies on
// the button's click event alone: a left click inside its rectangle (checked directly) starts listening too, and the
// key is read both from Input and from the GUI key events. Mouse buttons are never bound. Both steps are logged.
internal class KeyBindListener : MonoBehaviour
{
	private ConfigEntry<KeyCode> _entry;

	private TMP_Text _label;

	private Func<bool> _isListening;

	private Action<bool> _setListening;

	private RectTransform _rect;

	private int _startFrame = -1;

	// a key button is waiting: the panel mustn't close on Esc (that cancels the binding) or react to the key
	internal static bool Capturing;

	internal static int CaptureEndFrame = -1;

	internal void Init(ConfigEntry<KeyCode> entry, TMP_Text label, Func<bool> isListening, Action<bool> setListening)
	{
		_entry = entry;
		_label = label;
		_isListening = isListening;
		_setListening = setListening;
		_rect = GetComponent<RectTransform>();
	}

	internal void Begin()
	{
		if (_isListening == null || _isListening())
		{
			return;
		}
		_setListening(obj: true);
		Capturing = true;
		_startFrame = Time.frameCount;
		_label.text = "Press a key...";
		if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Config] Waiting for a key for " + _entry.Definition.Key + " (Esc cancels)");
	}

	private void Update()
	{
		if (_isListening == null)
		{
			return;
		}
		if (!_isListening())
		{
			if (Input.GetMouseButtonDown(0) && _rect != null && RectTransformUtility.RectangleContainsScreenPoint(_rect, Input.mousePosition, null))
			{
				Begin();
			}
			return;
		}
		if (Time.frameCount == _startFrame || !Input.anyKeyDown)
		{
			return;
		}
		foreach (KeyCode value in Enum.GetValues(typeof(KeyCode)))
		{
			if (value != KeyCode.None && !IsMouse(value) && Input.GetKeyDown(value))
			{
				Take(value);
				return;
			}
		}
	}

	// the GUI key event arrives even when something else is eating Input for the frame
	private void OnGUI()
	{
		if (_isListening == null || !_isListening() || Time.frameCount == _startFrame)
		{
			return;
		}
		Event e = Event.current;
		if (e != null && e.type == EventType.KeyDown && e.keyCode != KeyCode.None)
		{
			Take(e.keyCode);
			e.Use();
		}
	}

	private void Take(KeyCode key)
	{
		Capturing = false;
		CaptureEndFrame = Time.frameCount;
		if (key == KeyCode.Escape)
		{
			_label.text = _entry.Value.ToString();
			_label.color = new Color(0.83f, 0.52f, 0.18f, 1f);
			_setListening(obj: false);
			if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Config] Key binding for " + _entry.Definition.Key + " cancelled");
			return;
		}
		_entry.Value = key;
		_label.text = key.ToString();
		_label.color = new Color(0.83f, 0.52f, 0.18f, 1f);
		_setListening(obj: false);
		if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Config] " + _entry.Definition.Key + " = " + key);
	}

	private static bool IsMouse(KeyCode k)
	{
		return k >= KeyCode.Mouse0 && k <= KeyCode.Mouse6;
	}
}
