using System;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;

namespace Companions
{
    // ---- 1.4.94: after a companion kills something it goes over and picks up what the creature dropped, like a player
    // (user: "Right now if the NPC kills something it just goes on its way"). Only the kill's own drops: anything that
    // was already lying there is left alone. Drops that come out of the body a few seconds later (ragdolls) are waited for.
    public static class MHR_Loot
    {
        class Kill
        {
            public Vector3 Pos; public float Time; public string Name;
            public HashSet<int> Before = new HashSet<int>();
            public HashSet<int> Seen = new HashSet<int>();
            public HashSet<int> Skipped = new HashSet<int>();   // in deep water or out of reach (not "already there")
            public bool SawDrop; public float LastDrop; public float NextBodyCheck; public bool Body;
            public float Start; public float ArrivedAt = -1f; public float LastDrive;
        }
        class State { public Kill Current; public readonly List<Kill> Queue = new List<Kill>(); public ItemDrop Target; public float TargetSince; }

        static readonly Dictionary<int, State> s_state = new Dictionary<int, State>();
        static readonly Collider[] s_buf = new Collider[128];
        static int s_mask = -1;

        static int Mask() { if (s_mask == -1) s_mask = LayerMask.GetMask("item"); return s_mask; }
        static float Radius() { return ModConfig.LootRadius != null ? ModConfig.LootRadius.Value : 6f; }
        static float Wait() { return ModConfig.LootWait != null ? ModConfig.LootWait.Value : 10f; }
        static float MaxDist() { return ModConfig.LootMaxDistance != null ? ModConfig.LootMaxDistance.Value : 30f; }

        public static bool Active(Component c)
        {
            CompanionAI ai = c != null ? c.GetComponent<CompanionAI>() : null;
            State st;
            return ai != null && s_state.TryGetValue(ai.GetInstanceID(), out st) && st.Current != null;
        }

        // called when a creature this companion was after dies (its last hit, or Hunt's prey)
        public static void OnKill(CompanionSetup cs, Character victim)
        {
            if (cs == null || victim == null || victim.IsPlayer() || victim.GetComponent<CompanionSetup>() != null) return;
            OnKillAt(cs, victim.transform.position, victim.m_name, null);
        }

        // 1.4.100: also for things that aren't creatures (a shot bird). `before` = items that were lying there before
        // (null = take a look now)
        public static void OnKillAt(CompanionSetup cs, Vector3 pos, string name, HashSet<int> before)
        {
            if (cs == null) return;
            if (ModConfig.LootKills != null && !ModConfig.LootKills.Value) return;
            CompanionAI ai = cs.GetComponent<CompanionAI>();
            if (ai == null) return;
            int id = ai.GetInstanceID();
            State st;
            if (!s_state.TryGetValue(id, out st)) { st = new State(); s_state[id] = st; }
            if (st.Current != null && Vector3.Distance(st.Current.Pos, pos) < 1f) return;
            for (int i = 0; i < st.Queue.Count; i++) if (Vector3.Distance(st.Queue[i].Pos, pos) < 1f) return;
            Kill k = new Kill { Pos = pos, Time = Time.time, Name = name };
            if (before != null) k.Before.UnionWith(before);
            else k.Before.UnionWith(ItemsAround(pos));

            if (st.Queue.Count > 6) st.Queue.RemoveAt(0);
            st.Queue.Add(k);
        }

        // CompanionAI.UpdateAI, after combat: true while it's walking over to pick things up
        public static bool Drive(CompanionAI ai, float dt)
        {
            if (ai == null) return false;
            State st;
            if (!s_state.TryGetValue(ai.GetInstanceID(), out st)) return false;
            if (st.Current == null)
            {
                if (st.Queue.Count == 0) return false;
                st.Current = st.Queue[0]; st.Queue.RemoveAt(0); st.Target = null;
                // 1.4.95: timers count from when it sets off, not from the kill (1.4.94: "nothing dropped" a second
                // after setting off for a Neck 15 m away, before it got anywhere near)
                st.Current.Start = Time.time; st.Current.LastDrive = Time.time;
                Log("Going to pick up what " + Loc(st.Current.Name) + " dropped (" + Vector3.Distance(ai.transform.position, st.Current.Pos).ToString("F0") + " m away)");
            }
            Kill k = st.Current;
            // 1.4.96: a fight in between (1.4.95 test: Neck, then a boar straight after) doesn't use up its time
            if (Time.time - k.LastDrive > 0.5f)
            {
                float gap = Time.time - k.LastDrive;
                k.Start += gap; if (k.ArrivedAt >= 0f) k.ArrivedAt += gap; k.LastDrive = Time.time;
                LogDebug("back to what " + Loc(k.Name) + " dropped");
            }
            k.LastDrive = Time.time;
            CompanionSetup cs = ai.GetComponent<CompanionSetup>();
            Humanoid hum = ai.GetComponent<Humanoid>();
            Inventory inv = hum != null ? hum.GetInventory() : null;
            ZNetView nv = ai.GetComponent<ZNetView>();
            ZDO zdo = nv != null ? nv.GetZDO() : null;
            string why = null;
            Vector3 me = ai.transform.position;
            if (inv == null || zdo == null) why = "no pack";
            else if (!zdo.GetBool(CompanionSetup.AutoPickupHash, true)) why = "auto pickup is off";
            else if (Vector3.Distance(me, k.Pos) > MaxDist() + 5f) why = "too far away";
            else if (MHR_Owner.Of(cs) != null && cs != null && cs.GetFollow() && ai.GetFollowTarget() == MHR_Owner.Go(cs) &&
                     Vector3.Distance(MHR_Owner.Of(cs).transform.position, k.Pos) > MaxDist()) why = "you've moved on";
            else if (inv.GetTotalWeight() >= CompanionCarry.Max(inv) || inv.GetEmptySlots() == 0 && !RoomForAny(inv, k)) why = "pack is full";
            else if (Time.time - k.Start > Wait() + 25f) why = "took too long";
            if (why != null) { Finish(ai, st, "stopped picking up (" + why + ")"); return false; }

            // the body may still be lying there; its drops come out when it fades
            if (Time.time >= k.NextBodyCheck)
            {
                k.NextBodyCheck = Time.time + 1f;
                k.Body = false;
                try
                {
                    foreach (Ragdoll r in UnityEngine.Object.FindObjectsByType<Ragdoll>(FindObjectsSortMode.None))
                    {
                        if (r == null || !r.m_dropItems) continue;
                        Vector3 bp = r.GetAverageBodyPosition();
                        // 1.4.96: follow a body that slides down a slope or floats off; its drops come out where it ends up
                        if (Vector3.Distance(bp, k.Pos) < Radius() + 3f) { k.Body = true; k.Pos = bp; break; }
                    }
                }
                catch { }
            }

            if (st.Target != null)
            {
                ZNetView tv = st.Target.GetComponent<ZNetView>();
                if (tv == null || !tv.IsValid() || Time.time - st.TargetSince > 12f) st.Target = null;
            }
            if (st.Target == null)
            {
                st.Target = NextDrop(k, me, inv, ai.GetComponent<Character>());
                st.TargetSince = Time.time;
                if (st.Target != null) { k.SawDrop = true; k.LastDrop = Time.time; }
            }
            if (st.Target != null)
            {
                Vector3 tp = st.Target.transform.position;
                if (new Vector2(me.x - tp.x, me.z - tp.z).magnitude <= 1.6f && Mathf.Abs(me.y - tp.y) <= 2f)
                {
                    string name = st.Target.m_itemData != null ? st.Target.m_itemData.m_shared.m_name : "?";
                    int count = st.Target.m_itemData != null ? st.Target.m_itemData.m_stack : 0;
                    if (!st.Target.CanPickup()) { st.Target.RequestOwn(); ai.StopMoving(); return true; }
                    st.Target.Load();
                    if (hum.Pickup(st.Target.gameObject, false, false)) LogDebug("  picked up " + Loc(name) + " x" + count);
                    k.LastDrop = Time.time;
                    st.Target = null;
                    return true;
                }
                MHR_Mover.Go(ai, dt, tp, 0.8f, true, "Loot");
                // 1.4.97: never swim for loot (1.4.96 test: took a shortcut through the water to some drops)
                if (MHR_Mover.RouteSwim(ai) > NoSwim) { LogDebug("  " + Loc(st.Target.m_itemData != null ? st.Target.m_itemData.m_shared.m_name : "?") + " is across water - leaving it"); k.Skipped.Add(st.Target.GetInstanceID()); st.Target = null; MHR_Mover.Release(ai); return true; }
                if (MHR_Mover.GaveUp(ai)) { LogDebug("  can't get to " + Loc(st.Target.m_itemData != null ? st.Target.m_itemData.m_shared.m_name : "?") + " - leaving it"); k.Skipped.Add(st.Target.GetInstanceID()); st.Target = null; MHR_Mover.Release(ai); }
                return true;
            }

            // nothing to pick up right now: done, or go to the body and wait there for its drops
            // 1.4.97: a body in (or floating out into) deep water is watched from the bank, not swum to
            Character self = ai.GetComponent<Character>();
            Vector3 spot = DrySpot(self, me, k.Pos);
            float flat = new Vector2(me.x - spot.x, me.z - spot.z).magnitude;
            if (k.ArrivedAt < 0f && flat <= 4f) k.ArrivedAt = Time.time;
            bool there = k.ArrivedAt >= 0f;
            // 1.4.154: with no body found and nothing seen, still give the drops a few seconds from the kill to come out (log:
            // walked 17 m to a Neck, "nothing dropped" 2 s after getting there)
            bool done = (k.SawDrop && Time.time - k.LastDrop > 1.5f && !k.Body)
                     || (!k.SawDrop && !k.Body && there && Time.time - k.ArrivedAt > 2f && Time.time - k.Time > 8f)
                     || (there && Time.time - k.ArrivedAt > Wait());
            if (done) { Finish(ai, st, k.SawDrop ? "picked up everything" : "nothing dropped (waited " + (Time.time - k.Time).ToString("F0") + " s after the kill)" + Around(k)); return false; }
            if (!there)
            {
                MHR_Mover.Go(ai, dt, spot, 2f, true, "Loot");
                if (MHR_Mover.GaveUp(ai)) { Finish(ai, st, "stopped picking up (can't get to the body)"); return false; }
                if (MHR_Mover.RouteSwim(ai) > NoSwim) { Finish(ai, st, "stopped picking up (it's across water)"); return false; }
            }
            else ai.StopMoving();
            return true;
        }

        static void Finish(CompanionAI ai, State st, string what)
        {
            LogDebug(what);
            st.Current = null; st.Target = null;
            ai.StopMoving();
            MHR_Mover.Release(ai);
            if (st.Queue.Count == 0)
            {
                CompanionSetup cs = ai.GetComponent<CompanionSetup>();
                ZNetView nv = ai.GetComponent<ZNetView>();
                if (cs != null && nv != null && nv.GetZDO() != null) cs.ApplyFollowMode(nv.GetZDO().GetInt(CompanionSetup.ActionModeHash, 0), true);
            }
        }

        // for the log when nothing was found: what's lying around that it left alone (already there before the kill)
        static string Around(Kill k)
        {
            int n = Physics.OverlapSphereNonAlloc(k.Pos, Radius() * 2f, s_buf, Mask()), before = 0;
            for (int i = 0; i < n && i < s_buf.Length; i++) { ItemDrop d = DropOf(s_buf[i]); if (d != null && k.Before.Contains(d.GetInstanceID())) before++; }
            return before > 0 ? " (" + before + " item(s) nearby were already lying there before the kill)" : "";
        }

        // the last point on the way from me to p that isn't swimming depth
        static Vector3 DrySpot(Character self, Vector3 me, Vector3 p)
        {
            if (!MHR_Water.WouldSwim(self, p)) return p;
            Vector3 d = me - p; d.y = 0f;
            float len = d.magnitude;
            if (len < 0.5f) return me;
            d /= len;
            for (float x = 0.5f; x < len; x += 0.5f)
            {
                Vector3 q = p + d * x;
                if (!MHR_Water.WouldSwim(self, q)) { q.y = MHR_Water.GroundAt(q); return q; }
            }
            return me;
        }

        public static HashSet<int> ItemsAround(Vector3 pos)
        {
            HashSet<int> set = new HashSet<int>();
            int n = Physics.OverlapSphereNonAlloc(pos, Radius() * 2f + 2f, s_buf, Mask());
            for (int i = 0; i < n && i < s_buf.Length; i++)
            {
                ItemDrop d = DropOf(s_buf[i]);
                if (d != null) set.Add(d.GetInstanceID());
            }
            return set;
        }

        static ItemDrop DropOf(Collider col)
        {
            if (col == null || col.attachedRigidbody == null) return null;
            ItemDrop d = col.attachedRigidbody.GetComponent<ItemDrop>();
            if (d == null || !d.m_autoPickup) return null;
            ZNetView v = d.GetComponent<ZNetView>();
            return v != null && v.IsValid() ? d : null;
        }

        static bool RoomForAny(Inventory inv, Kill k)
        {
            int n = Physics.OverlapSphereNonAlloc(k.Pos, Radius(), s_buf, Mask());
            for (int i = 0; i < n && i < s_buf.Length; i++)
            {
                ItemDrop d = DropOf(s_buf[i]);
                if (d != null && !k.Before.Contains(d.GetInstanceID()) && d.m_itemData != null && inv.CanAddItem(d.m_itemData, 1)) return true;
            }
            return false;
        }

        // 1.4.95: wades in for drops in shallow water, but doesn't swim for them (user: tries not to get wet if he
        // doesn't need to). Drops it has seen that float off are still followed (within MaxDistance of the kill).
        static ItemDrop NextDrop(Kill k, Vector3 me, Inventory inv, Character self)
        {
            float look = k.ArrivedAt >= 0f ? Radius() * 2f : Radius();
            int n = Physics.OverlapSphereNonAlloc(k.Pos, k.Seen.Count > 0 ? MaxDist() : look, s_buf, Mask());
            ItemDrop best = null; float bd = float.MaxValue;
            for (int i = 0; i < n && i < s_buf.Length; i++)
            {
                ItemDrop d = DropOf(s_buf[i]);
                if (d == null || k.Before.Contains(d.GetInstanceID()) || k.Skipped.Contains(d.GetInstanceID()) || d.InTar()) continue;
                bool seen = k.Seen.Contains(d.GetInstanceID());
                if (!seen && Vector3.Distance(d.transform.position, k.Pos) > look) continue;
                if (MHR_Water.WouldSwim(self, d.transform.position))
                {
                    k.Skipped.Add(d.GetInstanceID());
                    LogDebug("  " + Loc(d.m_itemData != null ? d.m_itemData.m_shared.m_name : "?") + " is in deep water - leaving it");
                    continue;
                }
                k.Seen.Add(d.GetInstanceID());
                if (d.m_itemData == null || !inv.CanAddItem(d.m_itemData, 1)) continue;
                if (inv.GetTotalWeight() + d.m_itemData.GetWeight() > CompanionCarry.Max(inv)) continue;
                float dist = Vector3.Distance(me, d.transform.position);
                if (dist < bd) { bd = dist; best = d; }
            }
            return best;
        }

        // 1.5.75: loot is never worth a swim (user, log fd7b1683: "NPC jumped into the water and was swimming around. Maybe to
        // get the dropped items?" - it dived in for a Neck's drop, ran out of stamina, rested on the bank and went again).
        // Wading through shallow water is fine; any swimming on the way and the drop is left.
        const float NoSwim = 0.5f;

        static string Loc(string s) { try { return Localization.instance != null ? Localization.instance.Localize(s) : s; } catch { return s; } }
        static void Log(string msg) { if (CompanionsPlugin.Log != null) CompanionsPlugin.Log.LogInfo("[Loot] " + msg); }
        static void LogDebug(string msg) { if (CompanionsPlugin.Log != null) CompanionsPlugin.Log.LogDebug("[Loot] " + msg); }
    }

    // a creature dies: if a companion landed the last hit, it goes to collect the drops
    [HarmonyPatch(typeof(Character), "OnDeath")]
    public static class MHR_Loot_OnDeath_Patch
    {
        static void Prefix(Character __instance, HitData ___m_lastHit)
        {
            try
            {
                Character attacker = ___m_lastHit != null ? ___m_lastHit.GetAttacker() : null;
                if (attacker == null) return;
                CompanionSetup cs = attacker.GetComponent<CompanionSetup>();
                if (cs == null) return;
                ZNetView nv = cs.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid() || !nv.IsOwner()) return;
                MHR_Loot.OnKill(cs, __instance);
            }
            catch (Exception e) { if (CompanionsPlugin.Log != null) CompanionsPlugin.Log.LogWarning("[Loot] " + e.Message); }
        }
    }
}
