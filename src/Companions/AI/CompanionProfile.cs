using System;
using UnityEngine;

namespace Companions;

/// <summary>
/// Fase 3.10: the <b>AI profile of one companion</b> - the voice it speaks with and the personality it answers in -
/// kept in that companion's own ZDO, so two companions of the same player no longer talk and behave alike.
///
/// <para>Before this, voice and personality lived only in the <c>[IA]</c> section of the .cfg
/// (<c>TtsVoz</c>, <c>PresetPersonalidade</c>, <c>Personalidade</c>): every companion of the world spoke with the
/// same voice and answered in the same tone, and having several of them (the AI panel's summoner, the Trader) made
/// that obvious. The .cfg keeps being the <b>default</b> - a companion with nothing of its own here follows it
/// exactly as before, which is also what an old save (no keys) and every companion spawned by the Trader do.</para>
///
/// <para>Shape (the same shape the mod's other per-companion settings use, e.g. <c>HC_OrderCap</c> in
/// <see cref="CompanionSetup"/>): plain keys in the ZDO, written by the owner only and read anywhere. Two rules hold
/// the model together:</para>
/// <list type="bullet">
/// <item><b>the voice</b> is a number (42/43/44 are the pt-BR voices) and <b>0 means "no choice"</b> - there is no
/// pt-BR voice 0, and the panel only offers the three, so the value is never ambiguous;</item>
/// <item><b>the personality</b> is a preset id plus the player's free text (<see cref="CompanionPersona"/>), and it
/// needs an explicit "this companion has one" flag (<see cref="PersonaSetHash"/>): without it, "no choice" and "the
/// default personality" would both look like empty strings, and a companion could not keep the neutral tone while
/// the global preset is "viking".</item>
/// </list>
///
/// <para>Nothing here runs per frame: the profile is read when a line is spoken (once per message, on the main
/// thread - the ZDO is not thread-safe and the synthesis worker only ever sees the voice already resolved), when the
/// prompt of a message is built and when the panels are painted.</para>
/// </summary>
internal static class CompanionProfile
{
	/// <summary>The Kokoro speaker id this companion speaks with (0 = follow <c>[IA] TtsVoz</c>). The ZDO keys live in
	/// <see cref="CompanionSetup"/>, next to every other one.</summary>
	internal static readonly int VoiceHash = CompanionSetup.AiVoiceHash;

	/// <summary>True when the player gave <b>this</b> companion a personality of its own (preset and/or free text).</summary>
	internal static readonly int PersonaSetHash = CompanionSetup.AiPersonaSetHash;

	/// <summary>The preset id of its own personality ("viking", "mago"..., or "" for the default one).</summary>
	internal static readonly int PresetHash = CompanionSetup.AiPresetHash;

	/// <summary>The player's own words for this companion's personality (wins over the preset, as in the .cfg).</summary>
	internal static readonly int PersonaTextHash = CompanionSetup.AiPersonaTextHash;

	/// <summary>
	/// Id of the "first entry" of a per-companion list: <b>follow the <c>[IA]</c> default</b>. It is not a preset id
	/// (<see cref="CompanionPersona"/> never uses it) and not the empty string, which is a real choice - "the default
	/// personality" - for a companion whose player wants it neutral while the global preset is something else.
	/// </summary>
	internal const string InheritId = "*";

	// ------------------------------------------------------------------ voice

	/// <summary>The global voice (<c>[IA] TtsVoz</c>): the default of every companion and what the AI tab of the
	/// panel edits.</summary>
	internal static int GlobalVoice => CompanionTts.Voice;

	/// <summary>Did the player choose a voice for this companion (i.e. is something stored in its ZDO)?</summary>
	internal static bool HasVoice(Component c)
	{
		ZDO zdo = ZdoOf(c, out _);
		return zdo != null && zdo.GetInt(VoiceHash, 0) > 0;
	}

	/// <summary>The voice this companion speaks with: its own, or <c>[IA] TtsVoz</c> when it has none.</summary>
	internal static int GetVoice(Component c)
	{
		int voice = StoredVoice(c);
		return voice > 0 ? voice : GlobalVoice;
	}

	/// <summary>What is written in the ZDO (0 = nothing written, i.e. "follow the default").</summary>
	internal static int StoredVoice(Component c)
	{
		ZDO zdo = ZdoOf(c, out _);
		return zdo != null ? Mathf.Max(0, zdo.GetInt(VoiceHash, 0)) : 0;
	}

	/// <summary>Gives this companion its own voice. <paramref name="voice"/> 0 clears it (back to the default).</summary>
	internal static bool SetVoice(Component c, int voice)
	{
		int value = Mathf.Clamp(voice, 0, 100);
		if (StoredVoice(c) == value)
		{
			return true;   // already there - don't dirty the ZDO for nothing
		}
		return Write(c, delegate(ZDO zdo)
		{
			zdo.Set(VoiceHash, value);
		});
	}

	// ------------------------------------------------------------------ personality

	/// <summary>Did the player give this companion a personality of its own (preset and/or free text)?</summary>
	internal static bool HasPersona(Component c)
	{
		ZDO zdo = ZdoOf(c, out _);
		return zdo != null && zdo.GetBool(PersonaSetHash, false);
	}

	/// <summary>The preset that goes into the prompt: this companion's own, or <c>[IA] PresetPersonalidade</c>.</summary>
	internal static string GetPreset(Component c)
	{
		if (HasPersona(c))
		{
			return StoredPreset(c);
		}
		return ConfigPreset;
	}

	/// <summary>The free text that goes into the prompt: this companion's own, or <c>[IA] Personalidade</c>.</summary>
	internal static string GetFreeText(Component c)
	{
		if (HasPersona(c))
		{
			return StoredFreeText(c);
		}
		return ConfigFreeText;
	}

	/// <summary>The stored preset id (only meaningful with <see cref="HasPersona"/>; "" = the default personality).</summary>
	internal static string StoredPreset(Component c)
	{
		ZDO zdo = ZdoOf(c, out _);
		return zdo != null ? Trimmed(zdo.GetString(PresetHash, "")) : "";
	}

	/// <summary>The stored free text (only meaningful with <see cref="HasPersona"/>).</summary>
	internal static string StoredFreeText(Component c)
	{
		ZDO zdo = ZdoOf(c, out _);
		string text = zdo != null ? (zdo.GetString(PersonaTextHash, "") ?? "") : "";
		return text.Length > CompanionPersona.MaxFreeTextLength ? text.Substring(0, CompanionPersona.MaxFreeTextLength) : text;
	}

	/// <summary>
	/// Gives this companion a personality of its own. <see cref="InheritId"/> as the preset clears it (back to the
	/// default), and an empty preset with empty text is a real choice: the neutral default personality, whatever the
	/// global preset is. The free text wins over the preset when both are set, exactly as in <c>[IA]</c>.
	/// </summary>
	internal static bool SetPersona(Component c, string preset, string freeText)
	{
		string id = Trimmed(preset);
		if (string.Equals(id, InheritId, StringComparison.Ordinal))
		{
			return ClearPersona(c);
		}
		string text = freeText ?? "";
		if (text.Length > CompanionPersona.MaxFreeTextLength)
		{
			text = text.Substring(0, CompanionPersona.MaxFreeTextLength);
		}
		if (HasPersona(c) && string.Equals(StoredPreset(c), id, StringComparison.Ordinal) && string.Equals(StoredFreeText(c), text, StringComparison.Ordinal))
		{
			return true;   // already there - don't dirty the ZDO for nothing
		}
		return Write(c, delegate(ZDO zdo)
		{
			zdo.Set(PresetHash, id);
			zdo.Set(PersonaTextHash, text);
			zdo.Set(PersonaSetHash, true);
		});
	}

	/// <summary>Forgets this companion's own personality: it goes back to following the <c>[IA]</c> one.</summary>
	internal static bool ClearPersona(Component c)
	{
		if (!HasPersona(c) && StoredPreset(c).Length == 0 && StoredFreeText(c).Length == 0)
		{
			return true;   // nothing stored -> nothing to clear
		}
		return Write(c, delegate(ZDO zdo)
		{
			zdo.Set(PersonaSetHash, false);
			zdo.Set(PresetHash, "");
			zdo.Set(PersonaTextHash, "");
		});
	}

	/// <summary>The personality text that goes into the prompt of a message for this companion.</summary>
	internal static string PersonaText(Component c)
	{
		return CompanionPersona.Resolve(GetPreset(c), GetFreeText(c));
	}

	/// <summary>"viking (Viking leal)" / "texto livre" / "padrão" for the log and the panels, of this companion.</summary>
	internal static string DescribePersona(Component c)
	{
		return CompanionPersona.Describe(GetPreset(c), GetFreeText(c));
	}

	// ------------------------------------------------------------------ spawn

	/// <summary>
	/// Writes the profile a companion is born with (the choices of the starter panel). The ZDO has just been created
	/// and belongs to the local player, so there is no ownership dance here - and <b>nothing</b> is written for a
	/// choice the player left at "follow the default", so a new companion keeps following <c>[IA]</c> instead of
	/// being frozen on the values of that moment.
	/// </summary>
	internal static void ApplyAtSpawn(ZDO zdo, int voice, bool hasPersona, string preset, string freeText)
	{
		if (zdo == null)
		{
			return;
		}
		if (voice > 0)
		{
			zdo.Set(VoiceHash, Mathf.Clamp(voice, 0, 100));
		}
		if (!hasPersona)
		{
			return;
		}
		string id = Trimmed(preset);
		if (string.Equals(id, InheritId, StringComparison.Ordinal))
		{
			id = "";
		}
		string text = freeText ?? "";
		if (text.Length > CompanionPersona.MaxFreeTextLength)
		{
			text = text.Substring(0, CompanionPersona.MaxFreeTextLength);
		}
		zdo.Set(PresetHash, id);
		zdo.Set(PersonaTextHash, text);
		zdo.Set(PersonaSetHash, true);
	}

	// ------------------------------------------------------------------ the lists the panels show

	/// <summary>
	/// The preset ids of a personality dropdown, in the order it shows them. With <paramref name="withInherit"/> the
	/// list starts with <see cref="InheritId"/> ("follow the <c>[IA]</c> default") - that is the list of a
	/// <b>companion</b>; without it, the list is the global one of the AI tab, where the first entry is the "default"
	/// preset.
	/// </summary>
	internal static string[] PresetIds(bool withInherit)
	{
		CompanionPersona.Preset[] presets = CompanionPersona.Presets;
		int offset = withInherit ? 1 : 0;
		string[] ids = new string[presets.Length + 2 + offset];
		if (withInherit)
		{
			ids[0] = InheritId;
		}
		ids[offset] = "";
		for (int i = 0; i < presets.Length; i++)
		{
			ids[offset + 1 + i] = presets[i].Id;
		}
		ids[ids.Length - 1] = CompanionPersona.CustomId;
		return ids;
	}

	/// <summary>The localized names of <see cref="PresetIds"/>, index for index. With <paramref name="withInherit"/>
	/// the "default personality" entry gets its own wording ("neutral personality"), so it is never confused with the
	/// "follow the [IA] default" entry right above it (Fase 3.12).</summary>
	internal static string[] PresetLabels(bool withInherit)
	{
		CompanionPersona.Preset[] presets = CompanionPersona.Presets;
		int offset = withInherit ? 1 : 0;
		string[] labels = new string[presets.Length + 2 + offset];
		if (withInherit)
		{
			labels[0] = ModLocalization.Loc("hc_ai_profile_inherit");
		}
		labels[offset] = ModLocalization.Loc(withInherit ? "hc_ai_preset_neutral" : "hc_ai_preset_default");
		for (int i = 0; i < presets.Length; i++)
		{
			labels[offset + 1 + i] = ModLocalization.Loc(presets[i].NameKey);
		}
		labels[labels.Length - 1] = ModLocalization.Loc("hc_ai_preset_custom");
		return labels;
	}

	/// <summary>The voices of a voice dropdown: the pt-BR ones, with 0 ("follow the default") in front when
	/// <paramref name="withInherit"/> is on.</summary>
	internal static int[] VoiceIds(bool withInherit)
	{
		int count = CompanionTts.PortugueseVoiceCount;
		int offset = withInherit ? 1 : 0;
		int[] ids = new int[count + offset];
		for (int i = 0; i < count; i++)
		{
			ids[offset + i] = CompanionTts.PortugueseVoiceAt(i);
		}
		return ids;
	}

	/// <summary>The localized names of <see cref="VoiceIds"/>, index for index.</summary>
	internal static string[] VoiceLabels(bool withInherit)
	{
		int count = CompanionTts.PortugueseVoiceCount;
		int offset = withInherit ? 1 : 0;
		string[] labels = new string[count + offset];
		if (withInherit)
		{
			labels[0] = ModLocalization.Loc("hc_ai_profile_inherit");
		}
		for (int i = 0; i < count; i++)
		{
			labels[offset + i] = ModLocalization.Loc("hc_ai_voice_" + CompanionTts.PortugueseVoiceAt(i));
		}
		return labels;
	}

	// ------------------------------------------------------------------ plumbing

	private static string ConfigPreset
	{
		get
		{
			return ModConfig.AiPersonalityPreset != null ? Trimmed(ModConfig.AiPersonalityPreset.Value) : "";
		}
	}

	private static string ConfigFreeText
	{
		get
		{
			return ModConfig.AiPersonality != null ? Trimmed(ModConfig.AiPersonality.Value) : "";
		}
	}

	private static string Trimmed(string value)
	{
		return value == null ? "" : value.Trim();
	}

	private static ZDO ZdoOf(Component c, out ZNetView nv)
	{
		nv = c != null ? c.GetComponent<ZNetView>() : null;
		return (nv != null && nv.IsValid()) ? nv.GetZDO() : null;
	}

	/// <summary>
	/// Writes to the companion's ZDO. Everything in a ZDO is written by its owner, and a player choosing the voice or
	/// the personality of one of their companions is a deliberate command for that companion - so this claims the
	/// ZDO first, exactly like <see cref="MHR_Owner.TakeControl"/> does for an order, and only gives up when the
	/// claim does not stick (in multiplayer the previous owner's game may still hold it for a moment). A failed
	/// write is logged and returns false; the panels say so instead of showing a choice that was not saved.
	///
	/// <para>Fase 3.12 (auditoria): the ZDO is read <b>again</b> after the claim. Claiming can hand this game a new
	/// ZDO instance (the old one is a replica), and writing to the instance read before the claim would be lost while
	/// this method still answered "true". No ZDO after the claim = the write did not happen.</para>
	/// </summary>
	private static bool Write(Component c, Action<ZDO> write)
	{
		ZDO zdo = ZdoOf(c, out ZNetView nv);
		if (zdo == null || nv == null)
		{
			return false;
		}
		if (!nv.IsOwner())
		{
			nv.ClaimOwnership();
			// the claim may have replaced the ZDO this game was holding: whatever we write has to go to the one the
			// ZNetView has NOW, and only if the ownership really arrived
			zdo = nv.IsValid() ? nv.GetZDO() : null;
			if (!nv.IsOwner() || zdo == null)
			{
				CompanionsPlugin.Log?.LogWarning("[IA] Perfil do companheiro: sem posse do ZDO - a escolha nao foi gravada (tente de novo em instantes)");
				return false;
			}
		}
		write(zdo);
		return true;
	}
}

/// <summary>
/// Fase 3.10: the AI choices of the starter panel, waiting for the companion that is about to be born. The panel is
/// opened before the companion exists (it is the panel that creates it), so the choices are kept here and written to
/// the ZDO at the one point every spawn goes through (<c>CompanionManager.SpawnCompanion</c>, where the name and the
/// appearance are written too).
///
/// <para>A draft with nothing set is the normal case: the companion follows the <c>[IA]</c> values and keeps
/// following them, even if the player changes them later.</para>
/// </summary>
internal sealed class CompanionAiDraft
{
	/// <summary>Longest name the starter accepts: the HUD, the tabs of the AI panel and the hover all show it.</summary>
	internal const int MaxNameLength = 24;

	/// <summary>
	/// The name the player typed in the starter panel, or "" when the field was left empty (Fase 3.13). The spawn
	/// writes it as the companion's <c>HC_Name</c> when it has something in it; empty falls back to the default name of
	/// the tier, with the usual number when it is already taken (see <c>CompanionManager.NextDefaultName</c>).
	/// </summary>
	internal string Name = "";

	/// <summary>The voice the player picked (0 = follow <c>[IA] TtsVoz</c>).</summary>
	internal int Voice;

	/// <summary>True when the player chose a personality for this companion (preset and/or free text).</summary>
	internal bool HasPersona;

	/// <summary>The preset id ("" = the default personality; only meaningful with <see cref="HasPersona"/>).</summary>
	internal string Preset = "";

	/// <summary>The player's own words (wins over the preset; only meaningful with <see cref="HasPersona"/>).</summary>
	internal string FreeText = "";

	/// <summary>
	/// A typed name as it will be written: trimmed, and cut at <see cref="MaxNameLength"/>. A name of only spaces comes
	/// back as "", i.e. "the player left it empty" - the caller then keeps the default one.
	/// </summary>
	internal static string CleanName(string value)
	{
		string text = value == null ? "" : value.Trim();
		if (text.Length > MaxNameLength)
		{
			text = text.Substring(0, MaxNameLength).Trim();
		}
		return text;
	}

	/// <summary>Writes the draft into the ZDO of the companion that is being spawned.</summary>
	internal void WriteTo(ZDO zdo)
	{
		CompanionProfile.ApplyAtSpawn(zdo, Voice, HasPersona, Preset, FreeText);
	}

	internal bool IsEmpty => Voice <= 0 && !HasPersona;
}
