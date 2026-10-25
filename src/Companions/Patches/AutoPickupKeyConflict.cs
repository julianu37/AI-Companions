using System;
using HarmonyLib;
using UnityEngine;

namespace Companions;

/// <summary>
/// Fase 3.3b: the push-to-talk key fights with the game's own key.
///
/// <para>
/// How Valheim 1.0.17 reads the auto-pickup key (decompiled, <c>assembly_valheim</c> + <c>assembly_utils</c>):
/// </para>
/// <list type="number">
/// <item><c>ZInput.Reset()</c> creates the button - <c>AddButton("AutoPickup", KeyToPath(Key.V), rebindable: true)</c>
/// (ZInput.cs) - so <b>V</b> is the vanilla default and the player can rebind it (saved by the game as
/// <c>kbmBinding_AutoPickup</c>);</item>
/// <item><c>Player.Update</c> is the <b>only</b> place in the whole game that reads it:
/// <c>if (ZInput.GetButtonDown("AutoPickup") || flag7) { m_enableAutoPickup = !m_enableAutoPickup; Message(... "$hud_autopickup:" ...); }</c>;</item>
/// <item><c>ZInput.GetButtonDown(string name)</c> - a <b>static</b> method in <c>assembly_utils</c> - just asks the
/// button for its state: <c>m_instance?.TryGetButtonState(name, b =&gt; b.Pressed)</c>.</item>
/// </list>
///
/// <para>
/// So holding V to talk also flipped the player's auto-pickup. The fix is a Harmony <b>prefix on
/// <c>ZInput.GetButtonDown(string)</c></b> that answers <c>false</c> for <c>"AutoPickup"</c> while the mod's voice
/// owns the key. This is the safest of the options:
/// </para>
/// <list type="bullet">
/// <item>it does not touch the input system itself (the button is still read, pressed, released and shown in the
/// game's controls screen exactly as before - only this one query is answered "no");</item>
/// <item>it is <b>scoped to a single button name</b>: every other <c>GetButtonDown</c> call in the game falls through
/// untouched (the guard below is one string comparison), and <c>"AutoPickup"</c> has exactly one call site in the
/// whole game (Player.Update);</item>
/// <item>it is <b>conditional</b>: nothing happens unless <c>[AI] VoiceEnabled</c> is on <b>and</b> the game's AutoPickup
/// binding really points at <c>[AI] VoiceKey</c> - read from the game itself
/// (<c>ZInput.instance.GetButtonDef("AutoPickup").GetActionPath()</c>, the <i>effective</i> path, so a rebind counts).
/// If the player rebinds auto-pickup to another key, the mod never interferes.</item>
/// </list>
///
/// The player is told <b>once</b> (log + one line in the chat/HUD) that V now belongs to the push-to-talk and that the
/// auto-pickup can be moved to another key in the game's own controls screen.
/// </summary>
internal static class AutoPickupKeyConflict
{
	/// <summary>The game's button name for the auto-pickup toggle (the only name this class touches).</summary>
	internal const string AutoPickupButton = "AutoPickup";

	/// <summary>Resolution state: resolved once, retried until ZInput exists (the plugin loads before the game input).</summary>
	private static bool _resolved;

	private static bool _sharesKey;

	private static bool _warnedUnknownKey;

	private static bool _announced;

	/// <summary>
	/// True when the game's auto-pickup toggle must be swallowed: voice orders are on and the game's AutoPickup
	/// binding is the same key the push-to-talk listens to. Cheap and allocation-free (it is asked once per frame, by
	/// Player.Update, after the resolution).
	/// </summary>
	internal static bool BlocksAutoPickup
	{
		get
		{
			if (!VoiceInput.Enabled)
			{
				return false;
			}
			if (!_resolved && !Resolve())
			{
				return false;
			}
			return _sharesKey;
		}
	}

	/// <summary>
	/// Fase 3.7: the push-to-talk key can now be changed in the in-game AI panel, so the comparison above has to be
	/// redone after that - otherwise the auto-pickup toggle would keep being blocked (or stop being blocked) based on
	/// the key the mod had at startup. Called by the panel when <c>[AI] VoiceKey</c> changes; the next query resolves
	/// again and says so in the log.
	/// </summary>
	internal static void Reset()
	{
		_resolved = false;
		_announced = false;
	}

	/// <summary>
	/// Reads the key the game bound to "AutoPickup" and compares it with <c>[AI] VoiceKey</c>. False while the answer
	/// is still unknown (ZInput not up yet): the caller just doesn't block this frame and the check is retried.
	/// </summary>
	private static bool Resolve()
	{
		try
		{
			ZInput input = ZInput.instance;
			if (input == null)
			{
				return false;
			}
			ZInput.ButtonDef def = input.GetButtonDef(AutoPickupButton);
			string path = def?.GetActionPath();
			if (string.IsNullOrEmpty(path))
			{
				return false;
			}
			string mine = InputKeyName(VoiceInput.Key);
			if (mine == null)
			{
				// a key the mod can't name the Unity way: no comparison, so no blocking (the vanilla behaviour stays)
				_resolved = true;
				_sharesKey = false;
				if (!_warnedUnknownKey)
				{
					_warnedUnknownKey = true;
					CompanionsPlugin.Log?.LogWarning($"[STT] Nao sei traduzir a tecla do push-to-talk ([AI] VoiceKey = {VoiceInput.Key}) para o nome do Unity Input System, "
						+ $"entao nao vou mexer no toggle do auto-pickup do jogo (a tecla do jogo esta em \"{path}\").");
				}
				return true;
			}
			string bound = LastSegment(path);
			_sharesKey = string.Equals(bound, mine, StringComparison.OrdinalIgnoreCase);
			_resolved = true;
			if (_sharesKey)
			{
				AnnounceConflict(path, mine);
			}
			else
			{
				CompanionsPlugin.Log?.LogInfo($"[STT] Sem conflito de tecla: o auto-pickup do jogo esta em \"{bound}\" e o push-to-talk em \"{mine}\" - "
					+ "o toggle do auto-pickup continua funcionando normalmente.");
			}
			return true;
		}
		catch (Exception ex)
		{
			// never a reason to break the game's input: give up on the comparison and leave the vanilla behaviour
			_resolved = true;
			_sharesKey = false;
			CompanionsPlugin.Log?.LogWarning("[STT] Nao consegui comparar a tecla do auto-pickup com a do push-to-talk (o auto-pickup fica como no jogo): " + ex.Message);
			return true;
		}
	}

	private static void AnnounceConflict(string path, string key)
	{
		if (_announced)
		{
			return;
		}
		_announced = true;
		CompanionsPlugin.Log?.LogInfo($"[STT] CONFLITO DE TECLA: o auto-pickup do jogo esta em \"{path}\" - a MESMA tecla do push-to-talk "
			+ $"([AI] VoiceKey = {VoiceInput.Key}). Enquanto [AI] VoiceEnabled = true o mod BLOQUEIA o toggle do auto-pickup, para a tecla ficar com as "
			+ "ordens por voz. Se voce quiser o auto-pickup de volta, reatribua-o nas configuracoes do jogo (Configuracoes > Controles > Toggle "
			+ "Auto-Pickup) para outra tecla - ou desligue [AI] VoiceEnabled no .cfg.");
		try
		{
			// one line in the game too: the log alone is easy to miss
			CompanionChatAI.Announce(ModLocalization.LocFmt("hc_ai_voice_key_conflict", VoiceInput.Key.ToString()));
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogDebug("[STT] Nao consegui avisar o conflito de tecla no chat: " + ex.Message);
		}
	}

	/// <summary>"&lt;Keyboard&gt;/v" -> "v" (the key name the Unity Input System uses, as it appears in the path).</summary>
	private static string LastSegment(string path)
	{
		int cut = path.LastIndexOf('/');
		string value = cut >= 0 ? path.Substring(cut + 1) : path;
		return value.Trim().ToLowerInvariant();
	}

	/// <summary>
	/// The Unity Input System name of a KeyCode (the same spelling ZInput uses inside a binding path:
	/// <c>KeyToPath</c> writes <c>"&lt;Keyboard&gt;/" + key.ToString()</c>), or null for a key this table doesn't know -
	/// and then nothing is blocked. Only the common keys matter: the default is V and the game's own bindings are
	/// letters, digits and a handful of named keys.
	/// </summary>
	private static string InputKeyName(KeyCode key)
	{
		if (key >= KeyCode.A && key <= KeyCode.Z)
		{
			return ((char)('a' + (key - KeyCode.A))).ToString();
		}
		if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
		{
			return ((char)('0' + (key - KeyCode.Alpha0))).ToString();
		}
		if (key >= KeyCode.F1 && key <= KeyCode.F15)
		{
			return "f" + (key - KeyCode.F1 + 1);
		}
		if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9)
		{
			return "numpad" + (key - KeyCode.Keypad0);
		}
		switch (key)
		{
		case KeyCode.Space:
			return "space";
		case KeyCode.Tab:
			return "tab";
		case KeyCode.Return:
			return "enter";
		case KeyCode.Escape:
			return "escape";
		case KeyCode.Backspace:
			return "backspace";
		case KeyCode.Delete:
			return "delete";
		case KeyCode.Insert:
			return "insert";
		case KeyCode.Home:
			return "home";
		case KeyCode.End:
			return "end";
		case KeyCode.PageUp:
			return "pageUp";
		case KeyCode.PageDown:
			return "pageDown";
		case KeyCode.UpArrow:
			return "upArrow";
		case KeyCode.DownArrow:
			return "downArrow";
		case KeyCode.LeftArrow:
			return "leftArrow";
		case KeyCode.RightArrow:
			return "rightArrow";
		case KeyCode.LeftShift:
			return "leftShift";
		case KeyCode.RightShift:
			return "rightShift";
		case KeyCode.LeftControl:
			return "leftCtrl";
		case KeyCode.RightControl:
			return "rightCtrl";
		case KeyCode.LeftAlt:
			return "leftAlt";
		case KeyCode.RightAlt:
			return "rightAlt";
		case KeyCode.CapsLock:
			return "capsLock";
		case KeyCode.BackQuote:
			return "backquote";
		case KeyCode.Minus:
			return "minus";
		case KeyCode.Equals:
			return "equals";
		case KeyCode.LeftBracket:
			return "leftBracket";
		case KeyCode.RightBracket:
			return "rightBracket";
		case KeyCode.Backslash:
			return "backslash";
		case KeyCode.Semicolon:
			return "semicolon";
		case KeyCode.Quote:
			return "quote";
		case KeyCode.Comma:
			return "comma";
		case KeyCode.Period:
			return "period";
		case KeyCode.Slash:
			return "slash";
		case KeyCode.KeypadEnter:
			return "numpadEnter";
		case KeyCode.KeypadPeriod:
			return "numpadPeriod";
		case KeyCode.KeypadDivide:
			return "numpadDivide";
		case KeyCode.KeypadMultiply:
			return "numpadMultiply";
		case KeyCode.KeypadMinus:
			return "numpadMinus";
		case KeyCode.KeypadPlus:
			return "numpadPlus";
		default:
			return null;
		}
	}

	/// <summary>
	/// The patch itself: a prefix on the game's static <c>ZInput.GetButtonDown(string)</c>. For every button that is
	/// not "AutoPickup" it returns true immediately (the original method runs, nothing changes). For "AutoPickup" it
	/// answers <c>false</c> while the push-to-talk owns the key, so Player.Update never flips the auto-pickup and never
	/// shows the "$hud_autopickup" line.
	/// </summary>
	[HarmonyPatch(typeof(ZInput), "GetButtonDown")]
	private static class ZInput_GetButtonDown_Patch
	{
		private static bool Prefix(string name, ref bool __result)
		{
			// fast path: one string comparison per call (the game asks for many buttons every frame)
			if (!string.Equals(name, AutoPickupButton, StringComparison.Ordinal))
			{
				return true;
			}
			if (!BlocksAutoPickup)
			{
				return true;
			}
			__result = false;
			return false;
		}
	}
}
