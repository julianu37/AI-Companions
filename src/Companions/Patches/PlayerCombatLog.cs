using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Companions;

// 1.5.56: a temporary log of the PLAYER's own fighting, to model companions on it (user: "I do try to stay just out of
// range and then dip in to attack then back out. And block when needed to either parry or create an opening for an
// attack. I can swap to the bronze sword this time and do a few runs myself to get some of what I am doing into the
// logs."). Debug "[You] ..." lines: your swings (combo swing, gap to the enemy, stamina), guard up/down, dodges, blocks
// and parries, each enemy swing at you (how far you were, in or out of its reach, moving in/out), hits taken and dealt,
// and the spacing to the nearest enemy once a second. [Advanced] LogPlayerCombat turns it off.
internal static class PlayerCombatLog
{
	private static readonly Dictionary<int, bool> s_enemyInAttack = new Dictionary<int, bool>();

	private static bool s_blocking;

	private static bool s_dodging;

	private static float s_lastHp = -1f;

	private static float s_nextSpacing;

	private static float s_guardUpAt;

	private static string s_lastCombo;

	private static float s_lastSwingLog = -10f;

	private static readonly FieldInfo s_curAttack = AccessTools.Field(typeof(Humanoid), "m_currentAttack");

	private static readonly FieldInfo s_chainLevel = AccessTools.Field(typeof(Attack), "m_currentAttackCainLevel");

	internal static bool On => CompanionsPlugin.LogDetailed && (ModConfig.LogPlayerCombat?.Value ?? true);

	private static void Log(string s)
	{
		if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug("[You] " + s);
	}

	private static string Stam(Player p)
	{
		return $"stamina {p.GetStamina():F0}/{p.GetMaxStamina():F0}";
	}

	// gap from your edge to the enemy's edge, and the enemy's swing reach
	private static float Gap(Player p, Character c)
	{
		return Vector3.Distance(p.transform.position, c.transform.position) - p.GetRadius() - c.GetRadius();
	}

	private static Character Nearest(Player p, float range, out float gap)
	{
		Character best = null;
		gap = float.MaxValue;
		foreach (Character c in Character.GetAllCharacters())
		{
			if (c == null || c == p || c.GetHealth() <= 0f || !BaseAI.IsEnemy(p, c))
			{
				continue;
			}
			float g = Gap(p, c);
			if (g < range && g < gap)
			{
				gap = g;
				best = c;
			}
		}
		return best;
	}

	// + moving away from it, - closing in (m/s)
	private static float AwaySpeed(Player p, Character c)
	{
		Vector3 to = p.transform.position - c.transform.position;
		to.y = 0f;
		Vector3 v = p.GetVelocity();
		v.y = 0f;
		return to.sqrMagnitude > 0.0001f ? Vector3.Dot(v, to.normalized) : 0f;
	}

	internal static void Tick()
	{
		Player p = Player.m_localPlayer;
		if (!On || p == null || p.IsDead())
		{
			return;
		}
		float now = Time.time;
		// guard and dodge
		bool blocking = p.IsBlocking();
		if (blocking != s_blocking)
		{
			s_blocking = blocking;
			Character n = Nearest(p, 6f, out var g);
			if (blocking)
			{
				s_guardUpAt = now;
				if (CompanionsPlugin.LogDetailed) Log("Guard up" + (n != null ? $" - {n.m_name} {g:F1} m away (its reach {CompanionCombatAI.SwingReach(n):F1}){(n.InAttack() ? ", mid-swing" : "")}" : "") + ", " + Stam(p));
			}
			else
			{
				if (CompanionsPlugin.LogDetailed) Log($"Guard down after {now - s_guardUpAt:F2} s, " + Stam(p));
			}
		}
		bool dodging = p.InDodge();
		if (dodging && !s_dodging)
		{
			Character n = Nearest(p, 6f, out var g);
			if (CompanionsPlugin.LogDetailed) Log("Dodge roll" + (n != null ? $" - {n.m_name} {g:F1} m away{(n.InAttack() ? ", mid-swing" : "")}" : "") + ", " + Stam(p));
		}
		s_dodging = dodging;
		// hits taken
		float hp = p.GetHealth();
		if (s_lastHp >= 0f && hp < s_lastHp - 0.5f)
		{
			if (CompanionsPlugin.LogDetailed) Log($"Took {s_lastHp - hp:F0} damage ({hp:F0} hp left){(blocking ? ", guard up" : "")}");
		}
		s_lastHp = hp;
		// enemy swings at you
		foreach (Character c in Character.GetAllCharacters())
		{
			if (c == null || c == p || c.GetHealth() <= 0f || !BaseAI.IsEnemy(p, c))
			{
				continue;
			}
			int id = c.GetInstanceID();
			bool inAttack = c.InAttack();
			s_enemyInAttack.TryGetValue(id, out var was);
			if (inAttack && !was)
			{
				float g = Gap(p, c);
				if (g < 6f)
				{
					float reach = CompanionCombatAI.SwingReach(c);
					float away = AwaySpeed(p, c);
					bool atYou = c.GetBaseAI()?.GetTargetCreature() == p;
					if (CompanionsPlugin.LogDetailed) Log($"{c.m_name} swings{(atYou ? " at you" : "")} from {g:F1} m (its reach {reach:F1}) - you're {(g > reach ? "out of reach" : "in reach")}, {(away > 0.3f ? $"moving away {away:F1} m/s" : away < -0.3f ? $"closing in {-away:F1} m/s" : "standing")}{(blocking ? ", guard up" : "")}{(p.InAttack() ? ", mid-swing" : "")}, " + Stam(p));
				}
			}
			s_enemyInAttack[id] = inAttack;
		}
		if (s_enemyInAttack.Count > 200)
		{
			s_enemyInAttack.Clear();
		}
		// spacing once a second while something is close
		if (now >= s_nextSpacing)
		{
			s_nextSpacing = now + 1f;
			Character n = Nearest(p, 5f, out var g);
			if (n != null)
			{
				float away = AwaySpeed(p, n);
				if (CompanionsPlugin.LogDetailed) Log($"Spacing: {n.m_name} {g:F1} m (its reach {CompanionCombatAI.SwingReach(n):F1}{(n.InAttack() ? ", mid-swing" : "")}{(n.IsStaggering() ? ", staggered" : "")}), you're {(away > 0.3f ? $"moving away {away:F1} m/s" : away < -0.3f ? $"closing in {-away:F1} m/s" : "standing")}{(blocking ? ", guard up" : "")}, {Stam(p)}");
			}
		}
	}

	// your swings
	// Humanoid.StartAttack - Player doesn't override it (it runs for companions and monsters too; only yours is logged)
	[HarmonyPatch(typeof(Humanoid), "StartAttack")]
	private static class PlayerSwing
	{
		private static void Prefix(Humanoid __instance, out float __state)
		{
			__state = __instance != null ? __instance.GetTimeSinceLastAttack() : 0f;
		}

		private static void Postfix(Humanoid __instance, bool secondaryAttack, bool __result, float __state)
		{
			if (!__result || !On || __instance == null || __instance != Player.m_localPlayer)
			{
				return;
			}
			Player p = (Player)__instance;
			string combo = "";
			try
			{
				Attack cur = s_curAttack?.GetValue(__instance) as Attack;
				if (cur != null && cur.m_attackChainLevels > 1)
				{
					combo = $", combo swing {(int)s_chainLevel.GetValue(cur) + 1}/{cur.m_attackChainLevels}";
				}
			}
			catch
			{
			}
			// holding attack, the game calls StartAttack every frame until the swing shows as an attack (Player's queued
			// attack): log each swing once (log 9232441a: "combo swing 1/3" 3-8 times, 0.02 s apart)
			if (combo == s_lastCombo && Time.time - s_lastSwingLog < 0.4f)
			{
				return;
			}
			s_lastCombo = combo;
			s_lastSwingLog = Time.time;
			Character n = Nearest(p, 6f, out var g);
			string w = p.GetCurrentWeapon()?.m_shared?.m_name ?? "?";
			if (CompanionsPlugin.LogDetailed) Log($"{(secondaryAttack ? "Secondary attack" : "Swing")} with {w}{combo}, {__state:F2} s after the last swing ended" + (n != null ? $" - {n.m_name} {g:F1} m away (its reach {CompanionCombatAI.SwingReach(n):F1}){(n.InAttack() ? ", it's mid-swing" : "")}{(n.IsStaggering() ? ", it's staggered" : "")}" : "") + ", " + Stam(p));
		}
	}

	// your blocks and parries (Humanoid.BlockAttack runs for the player too)
	internal static void NoteBlock(Humanoid h, Character attacker, bool parry, float before, float after)
	{
		if (!On || h != Player.m_localPlayer)
		{
			return;
		}
		if (CompanionsPlugin.LogDetailed) Log($"{(parry ? "Parried" : "Blocked")} {(attacker != null ? attacker.m_name : "a hit")} {(s_guardUpAt > 0f ? $"{Time.time - s_guardUpAt:F2} s after raising the guard" : "")}: {before:F0} damage, {after:F0} got through, " + Stam((Player)h));
	}

	// your hits
	internal static void NoteHit(Character target, HitData hit)
	{
		if (!On || target == null)
		{
			return;
		}
		if (CompanionsPlugin.LogDetailed) Log($"Hit {target.m_name} for {hit.GetTotalDamage():F0}{(target.IsStaggering() ? " (staggered - double damage)" : "")}");
	}
}
