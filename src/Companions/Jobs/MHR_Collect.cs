using System;
using System.Collections.Generic;
using UnityEngine;

namespace Companions
{
    // ---- Fase 3.1: "recolhe os itens do chao" - the half of the order that was missing ----
    //
    // The wheel's Recolher button and the chat order "pickup" only ever flipped a flag in the companion's ZDO
    // (CompanionSetup.AutoPickupHash). That flag was read by MHR_Loot (the "go and pick up what the kill dropped"
    // job) and by the wheel's own label - and by nothing else: the magnet (CompanionAI.AutoPickup) never looked at
    // it, and no job in the mod ever walked to an item lying on the ground and picked it up. So "recolhe os itens do
    // chao" answered "Ok: recolher itens do chao" and nothing moved.
    //
    // On top of that, an item the companion (or the player) drops on purpose is marked m_autoPickup = false - which
    // is exactly what the game's own ItemDrop.OnPlayerDrop() does - so the magnet and MHR_Loot skip it on purpose
    // ("so neither it nor anyone's auto-pickup grabs it straight back"). With nothing else to pick it up, the item
    // the player had just asked to be dropped stayed on the ground for good.
    //
    // This job is that missing half. It is a one-shot ORDER, not a work mode: Start() is called by
    // CompanionOrders.Apply for the "pickup" action and CompanionAI.UpdateAI drives it every frame, exactly like
    // MHR_Loot. It walks to the nearest loose item inside [Looting] CollectRadius and takes it with
    // Humanoid.Pickup - the same call MHR_Loot, MHR_PileTaker, UpkeepPiles and MHR_SpearThrow use, and one that
    // does NOT look at m_autoPickup (checked in the decompiled game: only ItemDrop.CanPickup - the 0.5 s spawn
    // delay - and the inventory are consulted), so an item the companion dropped itself comes back. It repeats with
    // the next one until nothing is left inside the radius, the pack is full or the order runs out of time.
    //
    // The magnet and MHR_Loot are left exactly as they were, filter included: ONLY this explicit order takes what
    // was dropped on purpose.
    public static class MHR_Collect
    {
        class State
        {
            public bool Active;
            public float Start;              // when the order was given (shifted by the "fight in between" rule)
            public float LastDrive;
            public int StartMode;            // the work mode the companion was on when the order arrived
            public ItemDrop Target;
            public float TargetSince;
            public readonly HashSet<int> Skipped = new HashSet<int>();   // out of reach, deep water, refused
            public int Picked;               // how many items this order picked up
            public bool SawAny;              // at least one item was seen (even if it was refused)
            public bool NoRoom;              // something was there, but the pack had no space for it
        }

        static readonly Dictionary<int, State> s_state = new Dictionary<int, State>();
        static readonly Collider[] s_buf = new Collider[128];
        static int s_mask = -1;

        // How long the whole order is allowed to take. Like MHR_Loot, time in which the companion isn't driven (a
        // fight in between, a rest) doesn't count against it.
        const float Timeout = 30f;
        // The same reach MHR_Loot uses to decide "I'm standing on it"
        const float ReachFlat = 1.6f, ReachUp = 2f;

        static float Radius() { return ModConfig.CollectRadius != null ? ModConfig.CollectRadius.Value : 10f; }
        // 1.4.97's rule, kept: never swim for it (wading is fine). Same leash MHR_Loot uses for a directed walk
        // while following the player - if the owner has moved on and the item is far behind, the order gives up.
        static float Leash() { return ModConfig.LootMaxDistance != null ? ModConfig.LootMaxDistance.Value : 30f; }
        static int Mask() { if (s_mask == -1) s_mask = LayerMask.GetMask("item"); return s_mask; }

        /// <summary>Fase 3.1: the player asked for the ground to be picked up ("recolhe os itens do chao", or the
        /// wheel's Recolher). Called once per companion that takes the order; calling it again while one is already
        /// running simply starts it over.</summary>
        public static void Start(CompanionSetup cs)
        {
            if (cs == null) return;
            CompanionAI ai = cs.GetComponent<CompanionAI>();
            ZNetView nv = cs.GetComponent<ZNetView>();
            ZDO zdo = nv != null ? nv.GetZDO() : null;
            if (ai == null || zdo == null) return;
            int id = ai.GetInstanceID();
            State st;
            if (!s_state.TryGetValue(id, out st)) { st = new State(); s_state[id] = st; }
            st.Active = true;
            st.Start = Time.time;
            st.LastDrive = Time.time;
            st.StartMode = zdo.GetInt(CompanionSetup.ActionModeHash, 0);
            st.Target = null;
            st.TargetSince = 0f;
            st.Skipped.Clear();
            st.Picked = 0;
            st.SawAny = false;
            st.NoRoom = false;
            Log(ai, "going to pick up what's on the ground (up to " + Radius().ToString("F0") + " m away)");
        }

        /// <summary>Fase 3.1: the order is dropped (another order came in, or the companion was dismissed). Called
        /// from CompanionInteract.CancelExistingActions - the funnel the wheel uses to drop whatever a companion was
        /// on its way to do.</summary>
        public static void Cancel(Component c)
        {
            CompanionAI ai = c != null ? c.GetComponent<CompanionAI>() : null;
            if (ai == null) return;
            State st;
            if (!s_state.TryGetValue(ai.GetInstanceID(), out st) || !st.Active) return;
            Finish(ai, st, "Stopped: another order came in");
        }

        /// <summary>Fase 3.1: every collect order is dropped. Called once per message by CompanionOrders.Run when
        /// the order isn't "pickup" itself, so even the orders that never reach CompanionOrders.Apply (equip, drop,
        /// inventory) stop it.</summary>
        public static void CancelAll()
        {
            if (s_state.Count == 0) return;
            IReadOnlyList<CompanionSetup> all = CompanionSetup.AllCompanions;
            for (int i = 0; i < all.Count; i++)
            {
                Cancel(all[i]);
            }
        }

        /// <summary>True while this companion is out picking things up (for callers that want to know).</summary>
        public static bool Active(Component c)
        {
            CompanionAI ai = c != null ? c.GetComponent<CompanionAI>() : null;
            State st;
            return ai != null && s_state.TryGetValue(ai.GetInstanceID(), out st) && st.Active;
        }

        // CompanionAI.UpdateAI, right before MHR_Loot: true while it's walking over to pick something up
        public static bool Drive(CompanionAI ai, float dt)
        {
            if (ai == null) return false;
            State st;
            if (!s_state.TryGetValue(ai.GetInstanceID(), out st) || !st.Active) return false;

            // a fight in between (or anything else that kept UpdateAI from driving us) doesn't use up the order's time
            if (Time.time - st.LastDrive > 0.5f)
            {
                float gap = Time.time - st.LastDrive;
                st.Start += gap;
                if (st.TargetSince > 0f) st.TargetSince += gap;
                LogDebug(ai, "back to what's on the ground");
            }
            st.LastDrive = Time.time;

            CompanionSetup cs = ai.GetComponent<CompanionSetup>();
            Humanoid hum = ai.GetComponent<Humanoid>();
            Inventory inv = hum != null ? hum.GetInventory() : null;
            ZNetView nv = ai.GetComponent<ZNetView>();
            ZDO zdo = nv != null ? nv.GetZDO() : null;
            Character self = ai.GetComponent<Character>();
            int mode = zdo != null ? zdo.GetInt(CompanionSetup.ActionModeHash, 0) : 0;
            string why = null;
            if (hum == null || inv == null || zdo == null || self == null) why = "Stopped: no pack";
            else if (self.IsDead()) why = "Stopped: it's dead";
            else if (nv.IsValid() && !nv.IsOwner()) why = "Stopped: another player's game runs it now";
            else if (!zdo.GetBool(CompanionSetup.AutoPickupHash, true)) why = "Stopped: Recolher was turned off";
            else if (mode != st.StartMode && MHR_JobRules.IsJobMode(mode)) why = "Stopped: another job was picked";
            else if (Time.time - st.Start > Timeout) why = "Stopped: the order took too long";
            else if (OwnerMovedOn(cs, ai, st)) why = "Stopped: you've moved on";
            if (why != null) { Finish(ai, st, why); return false; }

            Vector3 me = ai.transform.position;
            // a target that is gone (picked up by someone else, destroyed) or that we have been after for too long
            if (st.Target != null)
            {
                ZNetView tv = st.Target.GetComponent<ZNetView>();
                if (tv == null || !tv.IsValid() || Time.time - st.TargetSince > 12f) st.Target = null;
            }
            if (st.Target == null)
            {
                st.Target = NextItem(ai, me, inv, self, st);
                st.TargetSince = Time.time;
                if (st.Target != null)
                {
                    Log(ai, "Going to pick up \"" + NameOf(st.Target) + "\" (" + Vector3.Distance(me, st.Target.transform.position).ToString("F0") + " m away)");
                }
            }
            if (st.Target == null)
            {
                // nothing left inside the radius: the order is done
                Finish(ai, st, null);
                return false;
            }

            ItemDrop d = st.Target;
            Vector3 tp = d.transform.position;
            float flat = new Vector2(me.x - tp.x, me.z - tp.z).magnitude;
            if (flat <= ReachFlat && Mathf.Abs(me.y - tp.y) <= ReachUp)
            {
                // Standing on it. Humanoid.Pickup is the same call the mod's other "go and take it" jobs use, and
                // it is what makes an m_autoPickup = false item (one that was dropped on purpose) come back.
                if (!d.CanPickup())
                {
                    d.RequestOwn();
                    ai.StopMoving();
                    return true;
                }
                d.Load();
                if (hum.Pickup(d.gameObject, autoequip: false, autoPickupDelay: false))
                {
                    st.Picked++;
                    LogDebug(ai, "  picked up \"" + NameOf(d) + "\"");
                }
                else
                {
                    // the game refused it (no room after all, or a quest item already known): don't come back to it
                    LogDebug(ai, "  couldn't take \"" + NameOf(d) + "\" - leaving it");
                    st.Skipped.Add(d.GetInstanceID());
                    st.NoRoom = true;
                }
                st.Target = null;
                return true;
            }

            MHR_Mover.Go(ai, dt, tp, 0.8f, true, "Collect");
            if (MHR_Mover.RouteSwim(ai) > MHR_Mover.JobSwim())
            {
                LogDebug(ai, "  \"" + NameOf(d) + "\" is a long swim away - leaving it");
                st.Skipped.Add(d.GetInstanceID());
                st.Target = null;
                MHR_Mover.Release(ai);
                return true;
            }
            if (MHR_Mover.GaveUp(ai))
            {
                LogDebug(ai, "  can't get to \"" + NameOf(d) + "\" - leaving it");
                st.Skipped.Add(d.GetInstanceID());
                st.Target = null;
                MHR_Mover.Release(ai);
            }
            return true;
        }

        // The leash: while it is following you, an item farther than [Looting] MaxDistance from YOU is one you have
        // moved on from (the same rule MHR_Loot applies to the drops of a kill).
        static bool OwnerMovedOn(CompanionSetup cs, CompanionAI ai, State st)
        {
            if (cs == null || st.Target == null || !cs.GetFollow()) return false;
            Player owner = MHR_Owner.Of(cs);
            if (owner == null || ai.GetFollowTarget() != MHR_Owner.Go(cs)) return false;
            return Vector3.Distance(owner.transform.position, st.Target.transform.position) > Leash();
        }

        // the nearest loose item inside the radius that this order may take
        static ItemDrop NextItem(CompanionAI ai, Vector3 me, Inventory inv, Character self, State st)
        {
            int n = Physics.OverlapSphereNonAlloc(me, Radius(), s_buf, Mask());
            ItemDrop best = null;
            float bestDist = float.MaxValue;
            bool room = false;
            st.NoRoom = false;
            for (int i = 0; i < n && i < s_buf.Length; i++)
            {
                ItemDrop d = DropOf(s_buf[i]);
                if (d == null) continue;
                // "something loose is lying there" - set before the filters, so the "nothing on the ground" line is
                // only said when the radius really was empty
                st.SawAny = true;
                int id = d.GetInstanceID();
                if (st.Skipped.Contains(id)) continue;
                if (!Allowed(ai, d, st)) continue;
                if (MHR_Water.WouldSwim(self, d.transform.position))
                {
                    st.Skipped.Add(id);
                    LogDebug(ai, "  \"" + NameOf(d) + "\" is in deep water - leaving it");
                    continue;
                }
                if (!Room(inv, d)) continue;
                room = true;
                float dist = Vector3.Distance(me, d.transform.position);
                if (dist < bestDist) { bestDist = dist; best = d; }
            }
            if (best == null) st.NoRoom = st.SawAny && !room;
            return best;
        }

        // What this order takes: anything loose the game itself would let a player pick up by hand PLUS the items
        // marked m_autoPickup = false (the ones the companion or the player dropped on purpose) - which is the whole
        // point of the order. What it refuses: quest items (the rule the drop order applies) and whatever a job that
        // gave up on the spot blacklisted (CompanionAI._targetBlacklist).
        static bool Allowed(CompanionAI ai, ItemDrop d, State st)
        {
            if (d.m_itemData == null || d.m_itemData.m_shared == null) return false;
            if (d.InTar()) return false;
            if (d.IsPiece()) return false;                    // the game's own auto-pickup refuses these too
            if (d.m_itemData.m_shared.m_questItem)
            {
                LogDebug(ai, "  \"" + NameOf(d) + "\" is a quest item - leaving it");
                st.Skipped.Add(d.GetInstanceID());
                return false;
            }
            return !ai.IsPositionBlacklisted(d.transform.position);
        }

        static bool Room(Inventory inv, ItemDrop d)
        {
            // The WHOLE pile has to fit: Humanoid.Pickup adds the whole stack in one go and refuses the item
            // otherwise (leaving it on the ground and showing the game's "$msg_noroom"), and CanAddItem(item) with
            // no number is exactly that question. Without this, an item whose stack doesn't fit would be picked as a
            // target over and over until the order timed out.
            if (d.m_itemData == null || !inv.CanAddItem(d.m_itemData)) return false;
            return inv.GetTotalWeight() + d.m_itemData.GetWeight() <= CompanionCarry.Max(inv);
        }

        // MHR_Loot's DropOf minus the m_autoPickup filter: THIS order is the one that takes what was dropped on
        // purpose. The magnet and MHR_Loot keep their own filter.
        static ItemDrop DropOf(Collider col)
        {
            if (col == null || col.attachedRigidbody == null) return null;
            ItemDrop d = col.attachedRigidbody.GetComponent<ItemDrop>();
            if (d == null) return null;
            ZNetView v = d.GetComponent<ZNetView>();
            return v != null && v.IsValid() ? d : null;
        }

        static void Finish(CompanionAI ai, State st, string why)
        {
            int picked = st.Picked;
            bool sawAny = st.SawAny;
            bool noRoom = st.NoRoom;
            s_state.Remove(ai.GetInstanceID());
            ai.StopMoving();
            MHR_Mover.Release(ai);
            // the order is over: back to following, exactly like MHR_Loot does when its queue runs out
            CompanionSetup cs = ai.GetComponent<CompanionSetup>();
            ZNetView nv = ai.GetComponent<ZNetView>();
            if (cs != null && nv != null && nv.GetZDO() != null)
            {
                cs.ApplyFollowMode(nv.GetZDO().GetInt(CompanionSetup.ActionModeHash, 0), true);
            }
            if (why != null)
            {
                Log(ai, why + " (" + picked + " item(s) picked up)");
                return;
            }
            if (picked > 0)
            {
                Log(ai, "Done: " + picked + " item(s) picked up" + (noRoom ? " (the pack is full)" : ""));
                return;
            }
            if (!sawAny)
            {
                Log(ai, "nothing on the ground within " + Radius().ToString("F0") + " m - nothing to pick up");
                Say(ai, "hc_ai_collect_nothing");
                return;
            }
            Log(ai, "nothing this order could take (the pack is full, or everything there is out of reach or a quest item)");
            if (noRoom) Say(ai, "hc_ai_collect_full");
        }

        static string NameOf(ItemDrop d)
        {
            string name = d != null && d.m_itemData != null && d.m_itemData.m_shared != null ? d.m_itemData.m_shared.m_name : "?";
            return Loc(name);
        }

        static string Loc(string s) { try { return Localization.instance != null ? Localization.instance.Localize(s) : s; } catch { return s; } }

        // the companion's own name, so a log with several of them can be read
        static string Who(CompanionAI ai)
        {
            ZDO zdo = ai != null ? ai.GetComponent<ZNetView>()?.GetZDO() : null;
            return (zdo != null ? zdo.GetString(CompanionSetup.NameHash, "Companion") : "Companion") + ": ";
        }

        static void Say(CompanionAI ai, string key)
        {
            try
            {
                CompanionTalk talk = ai != null ? ai.GetComponent<CompanionTalk>() : null;
                if (talk != null) talk.Say(ModLocalization.Loc(key), true);
            }
            catch (Exception e) { LogDebug(ai, "couldn't speak: " + e.Message); }
        }

        static void Log(CompanionAI ai, string msg) { if (CompanionsPlugin.Log != null) CompanionsPlugin.Log.LogInfo("[Collect] " + Who(ai) + msg); }
        static void LogDebug(CompanionAI ai, string msg) { if (CompanionsPlugin.Log != null) CompanionsPlugin.Log.LogDebug("[Collect] " + Who(ai) + msg); }
    }
}
