using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;

namespace Companions;

// 1.5.72: controller binds with an optional modifier (user: "These need to be able to be unmapped ... we need to be able use
// modifiers for the controller"). A bind is one of Choices; "Use" and "Alt" follow the game's controller layout (Alt is the
// game's own modifier, JoyAltKeys - the block button), the rest are fixed buttons. While a combo's modifier is held its
// button is hidden from the game (HidePatch) - only that button, only then: the modifier itself (block) always reaches the
// game, so blocking and the block + jump dodge are never touched.
internal static class MHR_Pad
{
	internal static readonly string[] Choices = new string[19]
	{
		"None", "Use", "Alt", "A", "B", "X", "Y", "LB", "RB", "LT",
		"RT", "DPadUp", "DPadDown", "DPadLeft", "DPadRight", "LStick", "RStick", "Back", "Start"
	};

	// the physical buttons, for capturing a bind in the settings panel
	internal static readonly string[] Physical = new string[16]
	{
		"A", "B", "X", "Y", "LB", "RB", "LT", "RT", "DPadUp", "DPadDown",
		"DPadLeft", "DPadRight", "LStick", "RStick", "Back", "Start"
	};

	internal static string Joy(string v)
	{
		switch (v)
		{
		case "Use": return "JoyUse";
		case "Alt": return "JoyAltKeys";
		case "A": return "JoyButtonA";
		case "B": return "JoyButtonB";
		case "X": return "JoyButtonX";
		case "Y": return "JoyButtonY";
		case "LB": return "JoyLBumper";
		case "RB": return "JoyRBumper";
		case "LT": return "JoyLTrigger";
		case "RT": return "JoyRTrigger";
		case "DPadUp": return "JoyDPadUp";
		case "DPadDown": return "JoyDPadDown";
		case "DPadLeft": return "JoyDPadLeft";
		case "DPadRight": return "JoyDPadRight";
		case "LStick": return "JoyLStick";
		case "RStick": return "JoyRStick";
		case "Back": return "JoyBack";
		case "Start": return "JoyStart";
		default: return null;
		}
	}

	// our own reads are never hidden
	private static bool s_own;

	private static bool Raw(string joy, int kind)
	{
		if (joy == null)
		{
			return false;
		}
		bool was = s_own;
		s_own = true;
		try
		{
			return kind == 0 ? ZInput.GetButtonDown(joy) : kind == 1 ? ZInput.GetButton(joy) : ZInput.GetButtonUp(joy);
		}
		finally
		{
			s_own = was;
		}
	}

	private static IEnumerable<KeyValuePair<ConfigEntry<string>, ConfigEntry<string>>> Binds()
	{
		yield return new KeyValuePair<ConfigEntry<string>, ConfigEntry<string>>(ModConfig.PadCommand, ModConfig.PadCommandModifier);
		yield return new KeyValuePair<ConfigEntry<string>, ConfigEntry<string>>(ModConfig.PadWheel, ModConfig.PadWheelModifier);
		yield return new KeyValuePair<ConfigEntry<string>, ConfigEntry<string>>(ModConfig.PadSettings, ModConfig.PadSettingsModifier);
		yield return new KeyValuePair<ConfigEntry<string>, ConfigEntry<string>>(ModConfig.PadFarmZone, ModConfig.PadFarmZoneModifier);
		yield return new KeyValuePair<ConfigEntry<string>, ConfigEntry<string>>(ModConfig.PadPassive, ModConfig.PadPassiveModifier);
		yield return new KeyValuePair<ConfigEntry<string>, ConfigEntry<string>>(ModConfig.PadBalanced, ModConfig.PadBalancedModifier);
		yield return new KeyValuePair<ConfigEntry<string>, ConfigEntry<string>>(ModConfig.PadRanged, ModConfig.PadRangedModifier);
		yield return new KeyValuePair<ConfigEntry<string>, ConfigEntry<string>>(ModConfig.PadMelee, ModConfig.PadMeleeModifier);
	}

	internal static bool IsSet(ConfigEntry<string> b)
	{
		return b != null && Joy(b.Value) != null;
	}

	internal static bool Plain(ConfigEntry<string> b, ConfigEntry<string> m, string value)
	{
		return b != null && b.Value == value && (m == null || Joy(m.Value) == null);
	}

	// the modifier is held - or, with none, no other combo on the same button has its modifier held (Alt + Y must not also
	// fire a plain Y bind)
	private static bool ModOk(ConfigEntry<string> b, ConfigEntry<string> m)
	{
		string mj = m != null ? Joy(m.Value) : null;
		if (mj != null)
		{
			return Raw(mj, 1);
		}
		string path = PathOf(Joy(b.Value));
		foreach (KeyValuePair<ConfigEntry<string>, ConfigEntry<string>> kv in Binds())
		{
			string om = kv.Value != null ? Joy(kv.Value.Value) : null;
			if (om != null && kv.Key != null && PathOf(Joy(kv.Key.Value)) == path && Raw(om, 1))
			{
				return false;
			}
		}
		return true;
	}

	internal static bool Down(ConfigEntry<string> b, ConfigEntry<string> m)
	{
		return IsSet(b) && Raw(Joy(b.Value), 0) && ModOk(b, m);
	}

	internal static bool Held(ConfigEntry<string> b, ConfigEntry<string> m)
	{
		return IsSet(b) && Raw(Joy(b.Value), 1) && ModOk(b, m);
	}

	internal static bool Up(ConfigEntry<string> b)
	{
		return IsSet(b) && Raw(Joy(b.Value), 2);
	}

	// ---- what the game is shown ----
	private static readonly AccessTools.FieldRef<ZInput, Dictionary<string, ZInput.ButtonDef>> s_buttons = AccessTools.FieldRefAccess<ZInput, Dictionary<string, ZInput.ButtonDef>>("m_buttons");

	private static readonly Dictionary<string, string> s_paths = new Dictionary<string, string>();

	private static float s_pathsAt = -100f;

	private static readonly HashSet<string> s_hidden = new HashSet<string>();

	private static int s_hiddenFrame = -1;

	internal static string PathOf(string joy)
	{
		if (joy == null || ZInput.instance == null)
		{
			return null;
		}
		float now = UnityEngine.Time.unscaledTime;
		if (now - s_pathsAt > 5f)
		{
			s_pathsAt = now;
			s_paths.Clear();
		}
		if (s_paths.TryGetValue(joy, out var p))
		{
			return p;
		}
		p = null;
		try
		{
			Dictionary<string, ZInput.ButtonDef> buttons = s_buttons(ZInput.instance);
			if (buttons != null && buttons.TryGetValue(joy, out var def) && def != null)
			{
				p = def.GetActionPath();
			}
		}
		catch
		{
		}
		s_paths[joy] = p;
		return p;
	}

	private static bool Hide(string name)
	{
		if (s_own || name == null || !name.StartsWith("Joy") || ZInput.instance == null)
		{
			return false;
		}
		int frame = UnityEngine.Time.frameCount;
		if (frame != s_hiddenFrame)
		{
			s_hiddenFrame = frame;
			s_hidden.Clear();
			foreach (KeyValuePair<ConfigEntry<string>, ConfigEntry<string>> kv in Binds())
			{
				string bj = kv.Key != null ? Joy(kv.Key.Value) : null;
				string mj = kv.Value != null ? Joy(kv.Value.Value) : null;
				if (bj != null && mj != null && Raw(mj, 1))
				{
					string path = PathOf(bj);
					if (path != null && path != PathOf(mj))
					{
						s_hidden.Add(path);
					}
				}
			}
		}
		if (s_hidden.Count == 0)
		{
			return false;
		}
		string np = PathOf(name);
		return np != null && s_hidden.Contains(np);
	}

	[HarmonyPatch(typeof(ZInput), "TryGetButtonState")]
	private static class HidePatch
	{
		private static bool Prefix(string name, ref bool __result)
		{
			if (Hide(name))
			{
				__result = false;
				return false;
			}
			return true;
		}
	}

	// ---- showing a bind ----
	internal static string Glyph(string v)
	{
		string joy = Joy(v);
		if (joy == null || ZInput.instance == null)
		{
			return v;
		}
		string s = ZInput.instance.GetBoundKeyString(joy, emptyStringOnMissing: true);
		if (string.IsNullOrEmpty(s))
		{
			return v;
		}
		return Localization.instance != null ? Localization.instance.Localize(s) : s;
	}

	// "[LT] + [Y]", or null when unbound
	internal static string Show(ConfigEntry<string> b, ConfigEntry<string> m)
	{
		if (!IsSet(b))
		{
			return null;
		}
		string s = Glyph(b.Value);
		return (m != null && Joy(m.Value) != null) ? Glyph(m.Value) + " + " + s : s;
	}

	internal static string GlyphOf(string joy)
	{
		if (ZInput.instance == null)
		{
			return joy;
		}
		string s = ZInput.instance.GetBoundKeyString(joy, emptyStringOnMissing: true);
		if (string.IsNullOrEmpty(s))
		{
			return joy;
		}
		return Localization.instance != null ? Localization.instance.Localize(s) : s;
	}
}
