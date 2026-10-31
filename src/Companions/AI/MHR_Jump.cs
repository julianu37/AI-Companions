using System;
using HarmonyLib;
using UnityEngine;

namespace Companions
{
    // ---- 1.4.98: jumps work like a player's (user: "use its actual speed and jump skill like a player... taking into
    // account its stamina"). The game's own Character.Jump keeps the speed you have going in, pushes up harder with a
    // higher Jump skill and gives a weaker jump with no stamina left - the same code for players and companions. What
    // was missing for companions: they paid no stamina for a jump (only Player.OnJump does), and our leaps across
    // streams and gaps set a fixed 6.5 m/s whatever the companion could actually do. ----
    public static class MHR_Jump
    {
        // what a jump costs, as for a player (Player.OnJump): base cost, changed by status effects, times the game's move stamina rate
        public static float Cost(Character c)
        {
            if (c == null) return 10f;
            float cost = c.m_jumpStaminaUsage;
            try { c.GetSEMan().ModifyJumpStaminaUsage(cost, ref cost); } catch { }
            return cost * Game.m_moveStaminaRate;
        }

        // enough stamina for a full-strength jump (with no stamina the game jumps at 70%)
        public static bool CanAfford(Character c)
        {
            if (c == null) return false;
            CompanionStamina st = c.GetComponent<CompanionStamina>();
            if (st != null) return st.Stamina >= Cost(c);
            return c.HaveStamina(Cost(c));
        }

        // a jump plus this share of its stamina left over (for jumps that don't end on dry ground)
        public static bool HasSpare(Character c, float share)
        {
            if (c == null) return false;
            CompanionStamina st = c.GetComponent<CompanionStamina>();
            if (st == null) return CanAfford(c);
            return st.Stamina >= Cost(c) + share * st.MaxStamina;
        }

        // how fast it runs right now: its run speed, with status effects (meads, wet, cold...), or faster if it's already moving faster
        public static float RunSpeed(Character c, Vector3 dir)
        {
            if (c == null) return 6f;
            float speed = c.m_runSpeed;
            try { c.GetSEMan().ApplyStatusEffectSpeedMods(ref speed, dir); } catch { }
            Vector3 v = c.GetVelocity(); v.y = 0f;
            return Mathf.Max(speed, v.magnitude);
        }

        // how far (flat meters) a running jump carries it when the landing is `dy` meters above (+) or below (-) the takeoff.
        // Same numbers as Character.Jump: jump force x (1 + 0.4 x Jump skill), 70% when out of stamina; 90% of that, close enough.
        public static float Reach(Character c, Vector3 dir, float dy)
        {
            if (c == null) return 2f;
            float skill = 0f;
            try { skill = c.GetSkillFactor(Skills.SkillType.Jump); } catch { }
            float boost = 1f + skill * 0.4f;
            float up = c.m_jumpForce * boost;
            float fwd = RunSpeed(c, dir) + c.m_jumpForceForward * boost;
            if (!CanAfford(c)) { up *= c.m_jumpForceTiredFactor; fwd *= c.m_jumpForceTiredFactor; }
            float g = Mathf.Max(1f, -Physics.gravity.y);
            float disc = up * up - 2f * g * dy;
            if (disc < 0f) return 0f;   // can't get that high
            float t = (up + Mathf.Sqrt(disc)) / g;
            return fwd * t * 0.9f;   // 1.4.99: close enough (user: it doesn't need to be perfect)
        }

        // the running leap itself: run speed going in, then the game's own jump (which keeps it)
        public static void Leap(CompanionAI ai, Character c, Vector3 dir)
        {
            ai.transform.rotation = Quaternion.LookRotation(dir);
            c.SetRun(true);
            Rigidbody rb = ai.GetComponent<Rigidbody>();
            float speed = RunSpeed(c, dir);
            if (rb != null) { Vector3 v = rb.linearVelocity; v.x = dir.x * speed; v.z = dir.z * speed; rb.linearVelocity = v; }
            c.Jump(false);
        }
    }

    // companions pay for a jump like a player does (Player.OnJump); vanilla only charges players
    [HarmonyPatch(typeof(Character), "OnJump")]
    public static class MHR_JumpStamina_Patch
    {
        static void Postfix(Character __instance)
        {
            try
            {
                if (__instance == null || __instance is Player) return;
                CompanionStamina st = __instance.GetComponent<CompanionStamina>();
                if (st == null) return;
                ZNetView nv = __instance.GetComponent<ZNetView>();
                if (nv == null || !nv.IsOwner()) return;
                st.UseStamina(MHR_Jump.Cost(__instance));
            }
            catch (Exception e) { if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug("[Jump] " + e.Message); }
        }
    }
}
