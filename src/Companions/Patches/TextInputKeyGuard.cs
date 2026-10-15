using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Companions;

/// <summary>
/// The registry of the mod's own text boxes and the per-frame "is one of them focused?" answer the key guard below
/// asks for.
///
/// <para>
/// Why this exists: a <c>TMP_InputField</c> only eats the keys it handles itself (typing, arrows, Ctrl+A/C/V/X, Esc,
/// Enter). Everything else the game reads straight from <c>ZInput</c> keeps working, so typing in the mod's text box
/// also fired the game's keys - the reported case was the free-text personality field of the AI panel: an <b>m</b> in
/// "fala como um <b>m</b>ago" opened the <b>map</b> (the map is toggled in <c>Minimap.Update</c>, which reads
/// <c>ZInput.GetButtonDown("Map")</c> and is <b>not</b> gated by <c>Player.TakeInput()</c> - so the
/// <c>TakeInput</c> guards the mod already had never covered it), and the same was true for Tab (inventory), F5
/// (console), Enter (chat), G, C, R, X, F, Q, E, the hotbar digits, and so on.
/// </para>
///
/// <para>
/// The fields are registered <b>once</b>, when they are built (<see cref="CompanionAiPanel"/> and
/// <see cref="CompanionInteractPanel"/>), and destroyed entries are dropped on the way. The focus state is
/// <b>computed once per frame</b> and cached for the rest of the frame: the guard runs inside
/// <c>ZInput.GetButtonDown</c>/<c>GetButton</c>/<c>GetKeyDown</c>, which the game calls dozens of times per frame,
/// so the first call of the frame pays for a two-entry loop and every other call is an <c>int</c> comparison.
/// </para>
/// </summary>
internal static class ModTextInputFocus
{
	private static readonly List<TMP_InputField> Fields = new List<TMP_InputField>(4);

	/// <summary>Frame the cached answer belongs to (<c>Time.frameCount</c>), -1 = nothing computed yet.</summary>
	private static int _frame = -1;

	private static bool _focused;

	/// <summary>Registers a text box of the mod. Called right after the field is built; safe to call twice.</summary>
	internal static void Register(TMP_InputField field)
	{
		if (field == null || Fields.Contains(field))
		{
			return;
		}
		Fields.Add(field);
	}

	/// <summary>
	/// Is any of the mod's text boxes focused right now? Cached per frame - the game asks many times per frame and
	/// the answer cannot change inside a frame in a way that matters.
	/// </summary>
	internal static bool AnyFocused
	{
		get
		{
			int frame = Time.frameCount;
			if (_frame == frame)
			{
				return _focused;
			}
			_frame = frame;
			_focused = Compute();
			return _focused;
		}
	}

	private static bool Compute()
	{
		bool focused = false;
		// backwards, so the destroyed entries are dropped while scanning (the list never grows past a couple of items)
		for (int i = Fields.Count - 1; i >= 0; i--)
		{
			TMP_InputField field = Fields[i];
			if (field == null)
			{
				Fields.RemoveAt(i);
				continue;
			}
			if (field.isFocused)
			{
				focused = true;
			}
		}
		return focused;
	}

	/// <summary>
	/// Is this ZInput button one of the gamepad ones? Every gamepad button of the game is named "Joy*" (ZInput.Reset
	/// registers them that way), and they are deliberately <b>left alone</b> while a text box is focused: the mod's
	/// own panels read "JoyBack"/"JoyButtonB" to close themselves, so blocking them would trap a gamepad player
	/// inside the panel. Keyboard and mouse buttons keep being swallowed.
	/// </summary>
	internal static bool IsGamepadButton(string name)
	{
		return name != null && name.Length >= 3 && name[0] == 'J' && name[1] == 'o' && name[2] == 'y';
	}

	/// <summary>
	/// The same idea for the low-level keys: a gamepad or mouse KeyCode is left alone, everything else (letters,
	/// digits, arrows, function keys, Esc, Tab...) is swallowed while a text box is focused. The gamepad test is the
	/// one ZInput itself uses.
	/// </summary>
	internal static bool IsGamepadOrMouseKey(KeyCode key)
	{
		if (key >= KeyCode.JoystickButton0)
		{
			return true;
		}
		return key >= KeyCode.Mouse0 && key <= KeyCode.Mouse4;
	}
}

/// <summary>
/// The guard itself: Harmony <b>prefixes</b> on the game's static <c>ZInput</c> readers that answer "not pressed"
/// while a text box of the mod is focused.
///
/// <list type="bullet">
/// <item>the fast path is one cached bool: with no mod text box focused (i.e. almost always) every one of these
/// methods runs exactly as before;</item>
/// <item>it is the same shape as <see cref="AutoPickupKeyConflict"/> (a conditional prefix on a ZInput reader, one
/// that never interferes when it is not its case and that fails open);</item>
/// <item><b>what is left alone:</b> the gamepad buttons/keys (see
/// <see cref="ModTextInputFocus.IsGamepadButton"/>) and the mouse methods (<c>GetMouseButton*</c> are separate
/// methods that are <b>not</b> patched) - the UI needs the mouse to click, and the gamepad has to be able to close
/// the panel;</item>
/// <item><b>what the UI needs keeps working:</b> the text box itself reads its keys through the legacy
/// <c>Input</c>/<c>Event</c> queue (TMP's <c>OnUpdateSelected</c>), never through <c>ZInput</c>, so typing, the
/// caret, the selection, Ctrl+A/C/V/X, Enter and Esc are untouched; the mod's own panels read their keys through
/// <c>Input.GetKeyDown</c> as well, so Esc/Enter/the push-to-talk key bind listener still work.</item>
/// </list>
/// </summary>
[HarmonyPatch(typeof(ZInput), "GetButtonDown")]
internal static class TextInputKeyGuard_GetButtonDown
{
	private static bool Prefix(string name, ref bool __result)
	{
		if (!ModTextInputFocus.AnyFocused || ModTextInputFocus.IsGamepadButton(name))
		{
			return true;
		}
		__result = false;
		return false;
	}
}

/// <summary>Held buttons ("Forward", "Left", "Run", "Crouch", "Attack"...): no walking or attacking while typing.</summary>
[HarmonyPatch(typeof(ZInput), "GetButton")]
internal static class TextInputKeyGuard_GetButton
{
	private static bool Prefix(string name, ref bool __result)
	{
		if (!ModTextInputFocus.AnyFocused || ModTextInputFocus.IsGamepadButton(name))
		{
			return true;
		}
		__result = false;
		return false;
	}
}

/// <summary>Released buttons, so a key held when the box got the focus is not "released" into the game.</summary>
[HarmonyPatch(typeof(ZInput), "GetButtonUp")]
internal static class TextInputKeyGuard_GetButtonUp
{
	private static bool Prefix(string name, ref bool __result)
	{
		if (!ModTextInputFocus.AnyFocused || ModTextInputFocus.IsGamepadButton(name))
		{
			return true;
		}
		__result = false;
		return false;
	}
}

/// <summary>Low-level keys (the map zoom, the debug keys, the split dialog digits...).</summary>
[HarmonyPatch(typeof(ZInput), "GetKeyDown")]
internal static class TextInputKeyGuard_GetKeyDown
{
	private static bool Prefix(KeyCode key, ref bool __result)
	{
		if (!ModTextInputFocus.AnyFocused || ModTextInputFocus.IsGamepadOrMouseKey(key))
		{
			return true;
		}
		__result = false;
		return false;
	}
}

/// <summary>Held low-level keys (Ctrl+click shortcuts and the like).</summary>
[HarmonyPatch(typeof(ZInput), "GetKey")]
internal static class TextInputKeyGuard_GetKey
{
	private static bool Prefix(KeyCode key, ref bool __result)
	{
		if (!ModTextInputFocus.AnyFocused || ModTextInputFocus.IsGamepadOrMouseKey(key))
		{
			return true;
		}
		__result = false;
		return false;
	}
}

/// <summary>Released low-level keys.</summary>
[HarmonyPatch(typeof(ZInput), "GetKeyUp")]
internal static class TextInputKeyGuard_GetKeyUp
{
	private static bool Prefix(KeyCode key, ref bool __result)
	{
		if (!ModTextInputFocus.AnyFocused || ModTextInputFocus.IsGamepadOrMouseKey(key))
		{
			return true;
		}
		__result = false;
		return false;
	}
}
