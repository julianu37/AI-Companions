using HarmonyLib;

namespace Companions;

[HarmonyPatch(typeof(TombStone), "UpdateDespawn")]
public static class TombstonePreventDespawnPatch
{
	private static readonly System.Reflection.MethodInfo s_floater = AccessTools.Method(typeof(TombStone), "UpdateFloater");

	private static readonly System.Reflection.MethodInfo s_positionCheck = AccessTools.Method(typeof(TombStone), "PositionCheck");

	// A7 (audit 1.9.4): this prefix fails OPEN. A silent fail-closed catch used to leave a companion's tombstone
	// suppressing the game's UpdateDespawn forever - it never emptied out, never despawned, and the log said nothing.
	// The warning is written once per session so a game update that renames a private method is loud but not spammy.
	private static bool _warnedFailOpen;

	[HarmonyPrefix]
	private static bool Prefix(TombStone __instance)
	{
		CompanionTombstoneResurrect component = __instance.GetComponent<CompanionTombstoneResurrect>();
		if (component == null)
		{
			return true;
		}
		// The reflection targets missing is the same failure as throwing: the two halves below would be skipped
		// and the game's despawn would stay suppressed. Let the game run its own UpdateDespawn instead.
		if (s_floater == null || s_positionCheck == null)
		{
			WarnFailOpen("UpdateFloater/PositionCheck not found");
			return true;
		}
		// 1.4.150: only the "empty -> remove" part is skipped; the game's floating in water and the fall-through-the-
		// ground check still run (a companion's tombstone sank or got lost under the terrain)
		try
		{
			if (__instance.m_floater != null)
			{
				s_floater.Invoke(__instance, null);
			}
			ZNetView nv = __instance.GetComponent<ZNetView>();
			if (nv != null && nv.IsValid() && nv.IsOwner())
			{
				s_positionCheck.Invoke(__instance, null);
			}
			return false;
		}
		catch (System.Exception ex)
		{
			WarnFailOpen(ex.Message);
			return true;
		}
	}

	private static void WarnFailOpen(string reason)
	{
		if (_warnedFailOpen)
		{
			return;
		}
		_warnedFailOpen = true;
		CompanionsPlugin.Log?.LogWarning("[Tombstone] Nao consegui rodar o UpdateDespawn do companheiro (" + reason
			+ ") - a lapide volta ao despawn normal do jogo.");
	}
}
