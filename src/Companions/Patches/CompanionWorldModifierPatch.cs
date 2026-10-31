using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Companions;

// 1.5.53: the Combat world modifier (and the world level) make enemies bigger and faster: Character.Awake scales any
// non-player by Game.m_enemySpeedSize (outdoors) and 1 + world level x m_worldLevelEnemyMoveSpeedMultiplier, and
// Character.UpdateWalking multiplies a non-player's speed by the same. Companions aren't players, so they grew too
// (bug report: "Turning the world modifier: combat to max makes the companions larger than the player. Their heads clip
// to the helmet when bigger."). Here those two values read as "no change" for a companion - same size and speed as you.
[HarmonyPatch]
internal static class CompanionWorldModifierPatch
{
	private static readonly FieldInfo s_speedSize = AccessTools.Field(typeof(Game), nameof(Game.m_enemySpeedSize));

	private static readonly FieldInfo s_worldLevel = AccessTools.Field(typeof(Game), nameof(Game.m_worldLevel));

	private static IEnumerable<MethodBase> TargetMethods()
	{
		yield return AccessTools.Method(typeof(Character), "Awake");
		yield return AccessTools.Method(typeof(Character), "UpdateWalking");
	}

	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
	{
		int replaced = 0;
		foreach (CodeInstruction ci in instructions)
		{
			if (ci.opcode == OpCodes.Ldsfld && (Equals(ci.operand, s_speedSize) || Equals(ci.operand, s_worldLevel)))
			{
				bool size = Equals(ci.operand, s_speedSize);
				// this.<helper>(): the same value for everyone else, the neutral one for a companion
				CodeInstruction load = new CodeInstruction(OpCodes.Ldarg_0);
				load.labels.AddRange(ci.labels);
				load.blocks.AddRange(ci.blocks);
				yield return load;
				yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CompanionWorldModifierPatch), size ? nameof(SpeedSize) : nameof(WorldLevel)));
				replaced++;
				continue;
			}
			yield return ci;
		}
		if (replaced == 0)
		{
			CompanionsPlugin.Log?.LogWarning("[WorldModifiers] " + __originalMethod?.Name + ": the enemy size/speed values weren't found - companions may grow with the Combat world modifier");
		}
	}

	// m_baseAI is set at the top of Character.Awake, before the scaling; a field read, cheap enough for every walking tick
	private static bool IsCompanion(Character c)
	{
		return c != null && c.GetBaseAI() is CompanionAI;
	}

	internal static float SpeedSize(Character c)
	{
		return IsCompanion(c) ? 1f : Game.m_enemySpeedSize;
	}

	internal static int WorldLevel(Character c)
	{
		return IsCompanion(c) ? 0 : Game.m_worldLevel;
	}
}
