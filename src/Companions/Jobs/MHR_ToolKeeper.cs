using System;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;

namespace Companions
{
    // ---- 1.4.62: fetch a missing job tool from a nearby chest, put borrowed ones back ----
    public static class MHR_ToolKeeper
    {
        const int K_AXE = 1, K_PICK = 2, K_BOW = 3, K_ARROW = 4, K_CULT = 5, K_ROD = 6, K_BAIT = 7, K_HAMMER = 8, K_FOOD = 9;
        const float SearchRadius = 30f, UseDist = 2.5f, TripTimeout = 150f, NoProgressTimeout = 45f;
        static readonly int BorrowHash = "HC_Borrowed".GetStableHashCode();

        class Entry { public int Kind; public string Prefab; public int Count; public Vector3 Chest; }
        class State
        {
            public List<Entry> Borrowed = new List<Entry>();
            public bool Loaded;
            public int Phase;              // 0 idle, 1 going to take, 2 going to return
            public Container Chest;
            public int Kind; public string Prefab; public Entry ReturnEntry;
            public float TripStart, NextDecide, NextSpeak, BestDist, LastProgress, NextTripLog;
            public Vector3 Goal; public bool NeedMove; public bool WasFighting; public bool HasSpot; public float NextSpotPick;
            public CraftingStation Station; public int WaitKind; public bool WaitHome; public float NextWaitSpeak, NextFollowFix;
            public int LastMode = -999; public bool UnloadPending, UnloadActive; public int UnloadTrips; public bool TagInit; public float NextUnloadSpeak; public bool WasAtHome;
            public Dictionary<int, float> KindCooldown = new Dictionary<int, float>();
            public Dictionary<int, float> RepairFailUntil = new Dictionary<int, float>();
            // 1.5.4: kinds the player took out of its pack for this job - it doesn't go and fetch another
            public HashSet<int> Refused = new HashSet<int>();
            public bool PanelOpen; public HashSet<int> PanelHad;
        }
        static readonly Dictionary<int, State> s_state = new Dictionary<int, State>();

        // 1.5.42: the hover status line for a trip in progress (or waiting to get home for a tool), else null
        internal static string TripText(Component c)
        {
            if (c == null) return null;
            CompanionSetup cs = c.GetComponent<CompanionSetup>();
            State st;
            if (cs == null || !s_state.TryGetValue(cs.GetInstanceID(), out st)) return null;
            string kn = KindName(st.Kind);
            switch (st.Phase)
            {
                case 1: return ModLocalization.LocFmt("hc_status_fetching", kn);
                case 2: return ModLocalization.LocFmt("hc_status_putting_back", kn);
                case 3: return ModLocalization.Loc("hc_status_unloading");
                case 4: return ModLocalization.Loc("hc_status_repairing_gear");
            }
            if (st.WaitHome) return ModLocalization.LocFmt("hc_status_needs_home", KindName(st.WaitKind));
            return null;
        }

        public static bool IsBusy(Component c)
        {
            if (c == null) return false;
            CompanionSetup cs = c.GetComponent<CompanionSetup>();
            if (cs == null) return false;
            State st;
            return s_state.TryGetValue(cs.GetInstanceID(), out st) && (st.Phase != 0 || st.UnloadActive || st.WaitHome);
        }

        // 1.4.76: Follow asks for this before a companion sits down indoors: put away what it picked up while
        // working (same rules as after a job change). Only does anything near home and with something to put away.
        public static void RequestUnload(CompanionSetup cs)
        {
            if (cs == null || !cs.HasHomePosition() || !AtHome(cs)) return;   // only at home, not at an outpost
            State st;
            if (!s_state.TryGetValue(cs.GetInstanceID(), out st) || st.UnloadPending || st.Phase != 0) return;
            Humanoid hum = cs.GetComponent<Humanoid>();
            Inventory inv = hum != null ? hum.GetInventory() : null;
            ZNetView nv = cs.GetComponent<ZNetView>();
            int mode = nv != null && nv.GetZDO() != null ? nv.GetZDO().GetInt(CompanionSetup.ActionModeHash, 0) : 0;
            if (inv == null || UnloadList(cs, st, inv, mode).Count == 0) return;
            st.UnloadPending = true; st.UnloadTrips = 0; st.NextDecide = 0f;
            LogDebug("Settling down near home - emptying bags first");
        }

        // 1.4.65: called from CompanionAI.UpdateAI after combat is handled. While a trip is on, the trip
        // is the only thing steering the companion (follow, patrol and job movement wait until it ends).
        public static bool Drive(CompanionAI ai, float dt)
        {
            if (ai == null) return false;
            // 1.4.88: out of a fire before following, idling or anything else
            if (MHR_Mover.EscapeFire(ai)) return true;
            CompanionSetup cs = ai.GetComponent<CompanionSetup>();
            if (cs == null) return false;
            State st;
            if (!s_state.TryGetValue(cs.GetInstanceID(), out st) || st.Phase == 0) return false;
            if (ai.GetFollowTarget() != null) ai.SetFollowTarget(null);
            if (st.NeedMove) MHR_Mover.Go(ai, dt, st.Goal, 0.6f, true, st.Phase == 4 ? "Repair" : "Tools");
            else ai.StopMoving();
            return true;
        }

        public static void Tick()
        {
            IReadOnlyList<CompanionSetup> all = CompanionSetup.AllCompanions;
            if (all == null || Player.m_localPlayer == null) return;
            float now = Time.time, dt = Time.deltaTime;
            for (int i = 0; i < all.Count; i++)
            {
                CompanionSetup cs = all[i];
                if (cs == null) continue;
                try { TickOne(cs, now, dt); }
                catch (Exception e)
                {
                    State st;
                    if (s_state.TryGetValue(cs.GetInstanceID(), out st)) st.Phase = 0;
                    if (CompanionsPlugin.Log != null) CompanionsPlugin.Log.LogWarning("[Tools] " + e.Message);
                }
            }
        }

        static void TickOne(CompanionSetup cs, float now, float dt)
        {
            ZNetView nv = cs.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || !nv.IsOwner()) return;
            ZDO zdo = nv.GetZDO();
            Humanoid hum = cs.GetComponent<Humanoid>();
            CompanionAI ai = cs.GetComponent<CompanionAI>();
            if (zdo == null || hum == null || ai == null || hum.IsDead()) return;
            int id = cs.GetInstanceID();
            s_current = cs;
            State st;
            if (!s_state.TryGetValue(id, out st)) { st = new State(); s_state[id] = st; }
            if (!st.Loaded) { Load(st, zdo); st.Loaded = true; }
            int mode = zdo.GetInt(CompanionSetup.ActionModeHash, 0);
            Inventory inv = hum.GetInventory();
            if (inv == null) return;
            MHR_Crossbow.Tick(hum);
            MHR_Mover.Tick(cs, ai);
            MHR_Follow.Tick(cs, ai);
            MHR_Recover.Tick(cs);   // 1.5.44: gear first after a revive (issue #17)

            // 1.4.70: gear, food and mead the player hands over (through the inventory panel) are the companion's own;
            // anything picked up during work isn't. Everything present at load counts as its own too.
            if (!st.TagInit || CompanionInteractPanel.IsOpenFor(cs)) { st.TagInit = true; TagOwn(inv); }
            if (st.LastMode == -999) st.LastMode = mode;
            if (mode != st.LastMode)
            {
                st.UnloadPending = true; st.UnloadTrips = 0;
                st.Refused.Clear();
                CompanionStatus.Clear(cs);   // 1.5.42: the old job's "why" no longer applies
                LogDebug("Job changed (" + st.LastMode + " -> " + mode +  ") - will empty bags at home");
                // 1.5.4: a fetch the new job has no use for is called off (user log: Hunt turned off, and he still walked
                // 50 m back to a chest for a bow, again after every recall)
                if (st.Phase == 1 && !NeedsKind(mode, st.Kind))
                {
                    Log("Not fetching " + KindName(st.Kind) + " any more - the job changed");
                    EndTrip(cs, st, ai, mode);
                }
            }
            st.LastMode = mode;
            // 1.5.4: the player took a tool the job uses out of its pack (user log: took the crossbow, and he went 58 m to a
            // chest for a hunting bow, then for another bow). Like a player handed that hint: work without it for this job.
            bool panelOpen = CompanionInteractPanel.IsOpenFor(cs);
            if (panelOpen && !st.PanelOpen) st.PanelHad = OwnedKinds(inv, mode);
            else if (!panelOpen && st.PanelOpen && st.PanelHad != null)
            {
                HashSet<int> now2 = OwnedKinds(inv, mode);
                foreach (int k in st.PanelHad)
                {
                    if (now2.Contains(k) || !st.Refused.Add(k)) continue;
                    string kn = KindName(k); kn = kn.StartsWith("an ") ? kn.Substring(3) : kn.StartsWith("a ") ? kn.Substring(2) : kn;
                    Log("You took its " + kn + " - it won't fetch another for this job");
                    // 1.5.42: and say so
                    cs.GetComponent<CompanionTalk>()?.Speak(ModLocalization.LocFmt("hc_why_tool_taken", kn), CompanionTalk.Priority.Event, "taken:" + k);
                    if (st.Phase == 1 && st.Kind == k) EndTrip(cs, st, ai, mode);
                }
                st.PanelHad = null;
            }
            st.PanelOpen = panelOpen;
            // 1.4.84: coming home with no job (or loading in at home) also empties the bags, like a player back from a
            // trip. It used to happen only on a job change, and that's forgotten on a restart (1.4.83 test: back home
            // with tin ore, bone fragments, meat... and he kept it all until a job was picked). 1.4.94: a hunter that
            // follows you keeps its catch until you're home, then puts it away the same way.
            bool atHome = cs.HasHomePosition() && AtHome(cs);
            if (atHome && !st.WasAtHome && (mode == 0 || (MHR_JobRules.KeepsFollow(mode) && (mode == 7 || cs.GetFollow()))) && !st.UnloadPending && st.Phase == 0 && UnloadList(cs, st, inv, mode).Count > 0)
            {
                st.UnloadPending = true; st.UnloadTrips = 0;
                LogDebug("Back home - will empty bags");
            }
            st.WasAtHome = atHome;

            if (st.Phase != 0) { UpdateTrip(cs, st, ai, hum, inv, zdo, mode, now, dt); return; }
            if (now < st.NextDecide) return;
            // 1.4.150: no new trip while an order is being carried out (starting one released the order's route)
            if (MHR_JobRules.OtherOrder(cs)) return;
            st.NextDecide = now + 1f;

            CompanionRest rest = cs.GetComponent<CompanionRest>();
            if (rest != null && (rest.IsSleeping || rest.IsNavigating)) return;
            if (ai.IsInCombat) return;

            // 1.4.71: chest and repair trips only near home; far away he follows you and sorts it out back home
            bool nearHome = NearHome(cs);
            if (st.WaitHome)
            {
                if (nearHome) { st.WaitHome = false; LogDebug("Back near home - sorting out the missing tool"); cs.ApplyFollowMode(mode, true); }
                else
                {
                    if (now >= st.NextFollowFix && MHR_Owner.Of(cs) != null && ai.GetFollowTarget() != MHR_Owner.Go(cs))
                    { st.NextFollowFix = now + 2f; ai.SetFollowTarget(MHR_Owner.Go(cs)); }
                    if (NeedsKind(mode, st.WaitKind) && BestInInventory(inv, st.WaitKind, null) == null) return;
                    st.WaitHome = false; cs.ApplyFollowMode(mode, true);
                }
            }
            if (!nearHome) goto FETCH;

            // 1) put back anything borrowed that the current job doesn't use (1.4.81: only at home, it can wait till then)
            if (!AtHome(cs)) goto FETCH;
            for (int b = 0; b < st.Borrowed.Count; b++)
            {
                Entry e = st.Borrowed[b];
                if (NeedsKind(mode, e.Kind)) continue;
                ItemDrop.ItemData have = FindByPrefab(inv, e.Prefab);
                if (have == null) { st.Borrowed.RemoveAt(b); Save(st, zdo); b--; continue; }
                float cd;
                if (st.KindCooldown.TryGetValue(-e.Kind, out cd) && now < cd) continue;
                Container target = FindChestByPos(e.Chest, have);
                if (target != null && !MHR_Chests.CanReach(cs.transform.position, target)) target = null;
                if (target == null) target = FindChestWithRoom(cs.transform.position, have);
                if (target == null)
                {
                    st.KindCooldown[-e.Kind] = now + 30f;
                    Log("Nowhere to put back borrowed \"" + e.Prefab + "\" - keeping it for now");
                    continue;
                }
                StartTrip(cs, st, ai, 2, target, e.Kind, e.Prefab, e, now);
                return;
            }

            // 1b) 1.4.70: after a job change (or coming home) put away what was picked up during work
            if (st.UnloadPending)
            {
                if (TryStartUnload(cs, st, ai, inv, mode, now)) return;
            }
            else st.UnloadActive = false;

            // 2) fetch anything the current job needs
            FETCH:
            int[] kinds = KindsFor(mode);
            if (kinds == null) return;
            ItemDrop.ItemData launcher = null;
            for (int k = 0; k < kinds.Length; k++)
            {
                int kind = kinds[k];
                if (st.Refused.Contains(kind)) continue;
                ItemDrop.ItemData owned = BestInInventory(inv, kind, launcher);
                if (owned != null) { if (kind == K_BOW || kind == K_ROD) launcher = owned; continue; }
                if ((kind == K_ARROW || kind == K_BAIT) && launcher == null) continue;
                float cd;
                if (st.KindCooldown.TryGetValue(kind, out cd) && now < cd) continue;
                ItemDrop.ItemData broken = BrokenOfKind(inv, kind, launcher);
                // 1.4.94: Hunt is bow first, melee second - no bow (or arrows) away from home isn't a reason to stop
                if (!nearHome && mode == 7) continue;
                // 1.4.117: Repair and Restock still restocks and sorts without a hammer - not a reason to stop and follow
                if (!nearHome && mode == 15) continue;
                if (kind == K_FOOD && !nearHome) continue;   // nice to have, not worth going home for
                if (!nearHome)
                {
                    // too far to go home for it: say so, stop the job for now and follow
                    st.WaitHome = true; st.WaitKind = kind;
                    Log((broken != null ? "\"" + broken.m_shared.m_name + "\" broke" : "Needs " + KindName(kind)) + " and home is too far - following until we're back");
                    if (now >= st.NextWaitSpeak) { st.NextWaitSpeak = now + 120f; if (broken != null) SpeakBroke(cs, kind); else Speak(cs, kind); }
                    if (MHR_Owner.Of(cs) != null) ai.SetFollowTarget(MHR_Owner.Go(cs));
                    return;
                }
                // near home: repair a broken one first
                float failUntil;
                bool repairFailed = st.RepairFailUntil.TryGetValue(kind, out failUntil) && now < failUntil;
                if (broken != null && !repairFailed)
                {
                    CraftingStation stn = FindRepairStation(cs, broken);
                    if (stn != null) { StartStationTrip(cs, st, ai, stn, kind, broken, now); return; }
                    LogDebug("No station near home can repair \"" + broken.m_shared.m_name + "\" - looking for another in the chests");
                }
                Vector3 center = cs.HasHomePosition() ? cs.GetHomePosition() : cs.transform.position;
                ItemDrop.ItemData found; Container chest = FindBestInChests(center, cs.transform.position, kind, launcher, out found);
                if (chest == null)
                {
                    st.KindCooldown[kind] = now + 60f;
                    if (broken != null)
                    {
                        // 1.4.74: broken, can't be repaired here and no spare: say so and keep following
                        Log("\"" + broken.m_shared.m_name + "\" is broken and there's no other in the chests - following you");
                        if (now >= st.NextSpeak) { SpeakBroke(cs, kind); st.NextSpeak = now + 120f; }
                        if (MHR_Owner.Of(cs) != null && ai.GetFollowTarget() == null) ai.SetFollowTarget(MHR_Owner.Go(cs));
                        continue;
                    }
                    Log("Needs " + KindName(kind) + " for this job and couldn't find one in nearby chests");
                    if (now >= st.NextSpeak) { Speak(cs, kind); st.NextSpeak = now + 60f; }
                    continue;
                }
                if (!RoomFor(inv, found, kind)) { st.KindCooldown[kind] = now + 30f; Log("No room to carry " + KindName(kind)); continue; }
                StartTrip(cs, st, ai, 1, chest, kind, found.m_dropPrefab != null ? found.m_dropPrefab.name : null, null, now);
                return;
            }
        }

        static void StartTrip(CompanionSetup cs, State st, CompanionAI ai, int phase, Container chest, int kind, string prefab, Entry entry, float now)
        {
            st.Phase = phase; st.Chest = chest; st.Kind = kind; st.Prefab = prefab; st.ReturnEntry = entry; st.TripStart = now;
            st.BestDist = float.MaxValue; st.LastProgress = now; st.NextTripLog = now + 5f;
            PickSpot(cs, st, chest, now); st.NeedMove = true; st.WasFighting = false;
            ai.SetFollowTarget(null);
            MHR_Mover.Release(ai);
            // 1.5.43 (phase 3): action lines - say where it's off to (once per kind of trip every few minutes)
            if (phase == 1 || phase == 2)
                cs.GetComponent<CompanionTalk>()?.Speak(ModLocalization.LocFmt(phase == 1 ? "hc_act_fetch_tool" : "hc_act_return_tool", KindName(kind)), CompanionTalk.Priority.Event, "act:tool" + phase + ":" + kind);
            Log((phase == 3 ? "Going to put away \"" + prefab + "\" and anything that stacks with it" : (phase == 1 ? "Going to fetch " : "Going to put back ") + KindName(kind) + " (\"" + prefab + "\")") + " at a chest " +
                Vector3.Distance(cs.transform.position, chest.transform.position).ToString("F0") + "m away");
        }

        static void EndTrip(CompanionSetup cs, State st, CompanionAI ai, int mode)
        {
            // 1.4.150: no SetInUse(false) here - the trip never sets it, and clearing it hid the player's open chest from the others
            st.Phase = 0; st.Chest = null; st.Station = null; st.ReturnEntry = null; st.NeedMove = false;
            ai.StopMoving();
            MHR_Mover.Release(ai);
            cs.ApplyFollowMode(mode, true);
        }

        static Vector3 ApproachPoint(Vector3 me, Vector3 cp)
        {
            Vector3 side = me - cp; side.y = 0f;
            return side.sqrMagnitude > 0.01f ? cp + side.normalized * 1.2f : cp;
        }

        // 1.4.66: stand somewhere we can walk to, with nothing solid between us and the chest
        static void PickSpot(CompanionSetup cs, State st, Container chest, float now)
        {
            st.NextSpotPick = now + 4f;
            Vector3 spot;
            if (MHR_Chests.PickApproach(cs.transform.position, chest, out spot)) { st.Goal = spot; st.HasSpot = true; }
            else { st.Goal = ApproachPoint(cs.transform.position, chest.transform.position); st.HasSpot = false; }
        }

        static void UpdateTrip(CompanionSetup cs, State st, CompanionAI ai, Humanoid hum, Inventory inv, ZDO zdo, int mode, float now, float dt)
        {
            if (st.Phase == 4) { UpdateStationTrip(cs, st, ai, inv, mode, now, dt); return; }
            // 1.4.65: a trip always finishes. Fighting only pauses it (the clocks stop while he fights).
            if (ai.IsInCombat)
            {
                st.TripStart += dt; st.LastProgress = now; st.BestDist = float.MaxValue;
                if (!st.WasFighting) { st.WasFighting = true; LogDebug("Trip paused - fighting"); }
                return;
            }
            if (st.WasFighting) { st.WasFighting = false; LogDebug("Trip resumed after the fight"); }
            float distNow = st.Chest != null ? Vector3.Distance(cs.transform.position, st.Chest.transform.position) : 0f;
            if (distNow < st.BestDist - 0.5f) { st.BestDist = distNow; st.LastProgress = now; }
            bool stalled = (st.NeedMove && MHR_Mover.GaveUp(ai)) || (now - st.LastProgress > NoProgressTimeout && !MHR_Mover.Progressing(ai));
            if (st.Chest == null || stalled || now - st.TripStart > TripTimeout)
            {
                st.KindCooldown[st.Phase == 1 ? st.Kind : -st.Kind] = now + 60f;
                Log("Gave up on the chest trip (" + (st.Chest == null ? "chest gone" : stalled ? "couldn't get any closer, " + distNow.ToString("F1") + "m away" : "took too long") + ")");
                EndTrip(cs, st, ai, mode);
                return;
            }
            if (now >= st.NextTripLog) { st.NextTripLog = now + 5f; LogDebug("Walking to chest: " + distNow.ToString("F1") + "m left"); }
            Vector3 cp = st.Chest.transform.position;
            Vector3 me = cs.transform.position;
            bool atSpot = st.HasSpot && new Vector2(me.x - st.Goal.x, me.z - st.Goal.z).magnitude <= 1.0f && Mathf.Abs(me.y - st.Goal.y) <= 1.5f;
            bool canReach = MHR_Chests.InReach(me, st.Chest);
            if (!atSpot && !canReach)
            {
                if (now >= st.NextSpotPick) PickSpot(cs, st, st.Chest, now);
                st.NeedMove = true;
                return;
            }
            if (!canReach && distNow > MHR_Chests.EyeReach + 0.5f) { st.NeedMove = true; return; }
            st.NeedMove = false;
            ai.StopMoving();
            ai.LookAtPoint(cp);
            // 1.4.152: not while someone has it open (here or on another player's game); this game owns it before the
            // contents change, so they're saved (MHR_Chests)
            if (MHR_Chests.InUse(st.Chest))
            {
                st.KindCooldown[st.Phase == 1 ? st.Kind : -st.Kind] = now + 30f;
                Log("Someone has the chest open - trying again later");
                EndTrip(cs, st, ai, mode);
                return;
            }
            MHR_Chests.Claim(st.Chest);
            cs.GetComponent<ZSyncAnimation>()?.SetTrigger("interact");
            Inventory cinv = st.Chest.GetInventory();
            if (cinv == null) { EndTrip(cs, st, ai, mode); return; }
            if (st.Phase == 3)
            {
                int moved = Deposit(cs, st, inv, cinv);
                Log("Put away " + moved + " item stack(s) in a chest");
            }
            else if (st.Phase == 1)
            {
                ItemDrop.ItemData launcher = st.Kind == K_ARROW ? BestInInventory(inv, K_BOW, null) : st.Kind == K_BAIT ? BestInInventory(inv, K_ROD, null) : null;
                ItemDrop.ItemData item = BestIn(cinv, st.Kind, launcher);
                if (item != null && RoomFor(inv, item, st.Kind))
                {
                    string prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : st.Prefab;
                    int count = item.m_stack;
                    if (st.Kind == K_FOOD && item.m_stack > 5)
                    {
                        // a few to eat, not the whole stack
                        ItemDrop.ItemData part = item.Clone(); part.m_stack = 5; count = 5;
                        if (inv.AddItem(part)) cinv.RemoveItem(item, 5);
                    }
                    else inv.MoveItemToThis(cinv, item);
                    if (FindByPrefab(inv, prefab) != null)
                    {
                        st.Borrowed.Add(new Entry { Kind = st.Kind, Prefab = prefab, Count = count, Chest = cp });
                        Save(st, zdo);
                        Log("Borrowed " + count + "x \"" + prefab + "\" for the job");
                    }
                }
                else { st.KindCooldown[st.Kind] = now + 60f; Log("The " + KindName(st.Kind) + " was gone from the chest"); }
            }
            else
            {
                Entry e = st.ReturnEntry;
                int left = e != null ? e.Count : 0;
                while (e != null && left > 0)
                {
                    ItemDrop.ItemData have = FindByPrefab(inv, e.Prefab);
                    if (have == null) break;
                    if (have.m_stack <= left)
                    {
                        int n = have.m_stack;
                        if (!cinv.CanAddItem(have, n)) break;
                        cinv.MoveItemToThis(inv, have);
                        if (FindSame(inv, have)) break;   // move refused
                        left -= n;
                    }
                    else
                    {
                        ItemDrop.ItemData part = have.Clone(); part.m_stack = left;
                        if (!cinv.CanAddItem(part, left)) break;
                        cinv.AddItem(part);
                        inv.RemoveItem(have, left);
                        left = 0;
                    }
                }
                if (e != null)
                {
                    if (left <= 0 || FindByPrefab(inv, e.Prefab) == null)
                    {
                        st.Borrowed.Remove(e); Save(st, zdo);
                        Log("Put back \"" + e.Prefab + "\"");
                    }
                    else { e.Count = left; Save(st, zdo); st.KindCooldown[-e.Kind] = now + 30f; Log("Chest full - kept " + left + "x \"" + e.Prefab + "\" for now"); }
                }
            }
            EndTrip(cs, st, ai, mode);
        }

        // ---- 1.4.71: home range, repairs ----
        public static float HomeRange() { return ModConfig.HomeTripRange != null ? ModConfig.HomeTripRange.Value : 60f; }
        // 1.4.81: "at home" for putting things away (bags, borrowed tools) and repairing barely-worn gear: inside the base,
        // not anywhere within HomeTripRange
        public static float AtHomeRange() { return Mathf.Min(25f, HomeRange()); }
        public static bool AtHome(CompanionSetup cs)
        {
            return cs == null || !cs.HasHomePosition() || Vector3.Distance(cs.transform.position, cs.GetHomePosition()) <= AtHomeRange();
        }
        public static bool NearHome(CompanionSetup cs)
        {
            return cs == null || !cs.HasHomePosition() || Vector3.Distance(cs.transform.position, cs.GetHomePosition()) <= HomeRange();
        }
        static ItemDrop.ItemData BrokenOfKind(Inventory inv, int kind, ItemDrop.ItemData launcher)
        {
            if (kind == K_ARROW || kind == K_BAIT || kind == K_FOOD) return null;
            List<ItemDrop.ItemData> items = inv.GetAllItems();
            for (int i = 0; i < items.Count; i++)
            {
                ItemDrop.ItemData it = items[i];
                if (it == null || it.m_shared == null || !it.m_shared.m_useDurability || it.m_durability > 0f) continue;
                if (IsKind(it, kind, launcher)) return it;
            }
            return null;
        }
        static CraftingStation FindRepairStation(CompanionSetup cs, ItemDrop.ItemData item)
        {
            Vector3 center = cs.HasHomePosition() ? cs.GetHomePosition() : cs.transform.position;
            CraftingStation best = null; float bd = float.MaxValue;
            List<CraftingStation> list = MHR_Chests.Stations(center, SearchRadius);
            for (int i = 0; i < list.Count; i++)
            {
                CraftingStation stn = list[i];
                bool ok;
                try { ok = RepairController.CanRepairAt(stn, item); } catch { ok = false; }
                if (!ok) continue;
                float d = Vector3.Distance(cs.transform.position, stn.transform.position);
                if (d < bd) { bd = d; best = stn; }
            }
            return best;
        }
        static void StartStationTrip(CompanionSetup cs, State st, CompanionAI ai, CraftingStation stn, int kind, ItemDrop.ItemData item, float now)
        {
            st.Phase = 4; st.Station = stn; st.Chest = null; st.Kind = kind; st.Prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : null; st.ReturnEntry = null;
            st.TripStart = now; st.BestDist = float.MaxValue; st.LastProgress = now; st.NextTripLog = now + 5f; st.WasFighting = false;
            st.Goal = ApproachPoint(cs.transform.position, stn.transform.position); st.NeedMove = true; st.HasSpot = false;
            ai.SetFollowTarget(null);
            MHR_Mover.Release(ai);
            Log("Going to repair \"" + item.m_shared.m_name + "\" at " + stn.m_name + " " + Vector3.Distance(cs.transform.position, stn.transform.position).ToString("F0") + "m away");
        }
        static void UpdateStationTrip(CompanionSetup cs, State st, CompanionAI ai, Inventory inv, int mode, float now, float dt)
        {
            if (ai.IsInCombat) { st.TripStart += dt; st.LastProgress = now; st.BestDist = float.MaxValue; return; }
            if (st.Station == null) { Log("The station is gone"); EndTrip(cs, st, ai, mode); return; }
            Vector3 me = cs.transform.position, sp = st.Station.transform.position;
            float dist = MHR_Nav.ReachDist(me, sp);
            if (dist < st.BestDist - 0.5f) { st.BestDist = dist; st.LastProgress = now; }
            if ((st.NeedMove && MHR_Mover.GaveUp(ai)) || (now - st.LastProgress > NoProgressTimeout && !MHR_Mover.Progressing(ai)) || now - st.TripStart > TripTimeout)
            {
                // 1.4.74: couldn't get to the station - look for another tool in the chests instead
                st.RepairFailUntil[st.Kind] = now + 300f;
                st.KindCooldown[st.Kind] = now + 2f;
                Log("Gave up on the repair trip (" + dist.ToString("F1") + "m away) - looking for another in the chests");
                EndTrip(cs, st, ai, mode); return;
            }
            if (dist > 2.5f) { st.NeedMove = true; return; }
            st.NeedMove = false; ai.StopMoving();
            int fixedCount = 0;
            List<ItemDrop.ItemData> items = inv.GetAllItems();
            for (int i = 0; i < items.Count; i++)
            {
                ItemDrop.ItemData it = items[i];
                if (it == null || it.m_shared == null || !it.m_shared.m_useDurability || it.m_durability >= it.GetMaxDurability()) continue;
                bool ok;
                try { ok = RepairController.CanRepairAt(st.Station, it); } catch { ok = false; }
                if (!ok) continue;
                it.m_durability = it.GetMaxDurability();
                fixedCount++;
            }
            try { if (fixedCount > 0 && st.Station.m_repairItemDoneEffects != null) st.Station.m_repairItemDoneEffects.Create(sp, Quaternion.identity); } catch { }
            Log("Repaired " + fixedCount + " item(s) at " + st.Station.m_name);
            EndTrip(cs, st, ai, mode);
        }
        static void SpeakBroke(CompanionSetup cs, int kind)
        {
            string key = kind == K_AXE ? "hc_speech_broke_axe" : kind == K_PICK ? "hc_speech_broke_pickaxe" : "hc_speech_broke_tool";
            CompanionTalk t = cs.GetComponent<CompanionTalk>();
            if (t != null) t.SayProblem("broke:" + kind, ModLocalization.Loc(key));
        }

        // ---- 1.4.70: emptying bags ----
        const string OwnKey = "HC_Own";
        static bool Keepable(ItemDrop.ItemData it)
        {
            if (it == null || it.m_shared == null) return false;
            switch (it.m_shared.m_itemType)
            {
                case ItemDrop.ItemData.ItemType.OneHandedWeapon: case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft: case ItemDrop.ItemData.ItemType.Bow:
                case ItemDrop.ItemData.ItemType.Shield: case ItemDrop.ItemData.ItemType.Helmet: case ItemDrop.ItemData.ItemType.Chest:
                case ItemDrop.ItemData.ItemType.Legs: case ItemDrop.ItemData.ItemType.Hands: case ItemDrop.ItemData.ItemType.Shoulder:
                case ItemDrop.ItemData.ItemType.Utility: case ItemDrop.ItemData.ItemType.Tool: case ItemDrop.ItemData.ItemType.Torch:
                case ItemDrop.ItemData.ItemType.Ammo: case ItemDrop.ItemData.ItemType.AmmoNonEquipable:
                case ItemDrop.ItemData.ItemType.Consumable: case ItemDrop.ItemData.ItemType.Trinket:
                    return true;
            }
            return false;
        }
        static void TagOwn(Inventory inv)
        {
            List<ItemDrop.ItemData> items = inv.GetAllItems();
            for (int i = 0; i < items.Count; i++)
            {
                ItemDrop.ItemData it = items[i];
                if (!Keepable(it)) continue;
                if (it.m_customData == null) it.m_customData = new Dictionary<string, string>();
                it.m_customData[OwnKey] = "1";
            }
        }
        static bool IsOwn(ItemDrop.ItemData it)
        {
            string v;
            return it.m_customData != null && it.m_customData.TryGetValue(OwnKey, out v) && v == "1";
        }
        static bool ShouldUnload(CompanionSetup cs, State st, ItemDrop.ItemData it, int mode)
        {
            if (it == null || it.m_dropPrefab == null || it.m_equipped) return false;
            if (Keepable(it) && IsOwn(it)) return false;
            string name = it.m_dropPrefab.name;
            for (int b = 0; b < st.Borrowed.Count; b++) if (st.Borrowed[b].Prefab == name) return false;   // borrowed tools go back on their own
            if (mode == 8 && FarmController.s_seedToPlant != null && FarmController.s_seedToPlant.ContainsKey(name)) return false;   // still farming
            return true;
        }
        static List<ItemDrop.ItemData> UnloadList(CompanionSetup cs, State st, Inventory inv, int mode)
        {
            List<ItemDrop.ItemData> list = new List<ItemDrop.ItemData>();
            List<ItemDrop.ItemData> items = inv.GetAllItems();
            for (int i = 0; i < items.Count; i++) if (ShouldUnload(cs, st, items[i], mode)) list.Add(items[i]);
            return list;
        }

        static bool TryStartUnload(CompanionSetup cs, State st, CompanionAI ai, Inventory inv, int mode, float now)
        {
            Vector3 me = cs.transform.position;
            bool hasHome = cs.HasHomePosition();
            Vector3 home = hasHome ? cs.GetHomePosition() : me;
            // 1.4.81: only when actually at home (25 m), not anywhere within HomeTripRange - he walked 54 m back to put
            // away a stack of stone when the job was switched off out in the field (1.4.80 test)
            if (hasHome && Vector3.Distance(me, home) > AtHomeRange()) { st.UnloadActive = false; return false; }   // later, when back home
            List<ItemDrop.ItemData> items = UnloadList(cs, st, inv, mode);
            if (items.Count == 0 || st.UnloadTrips >= 10)
            {
                if (items.Count > 0) Log("Couldn't find room for everything - keeping " + items.Count + " stack(s) for now");
                st.UnloadPending = false; st.UnloadActive = false; return false;
            }
            for (int i = 0; i < items.Count; i++)
            {
                ItemDrop.ItemData it = items[i];
                Container c = ChestFor(home, me, it);
                if (c == null) continue;
                st.UnloadActive = true;
                st.UnloadTrips++;
                if (now >= st.NextUnloadSpeak) { st.NextUnloadSpeak = now + 120f; CompanionTalk t = cs.GetComponent<CompanionTalk>(); if (t != null) t.Say(ModLocalization.Loc("hc_speech_unload"), false); }
                StartTrip(cs, st, ai, 3, c, 0, it.m_dropPrefab.name, null, now);
                return true;
            }
            Log("No chest near home has room for what I'm carrying - keeping " + items.Count + " stack(s)");
            CompanionStatus.Why(cs, "hc_why_chests_full", speak: true);
            st.UnloadPending = false; st.UnloadActive = false;
            return false;
        }

        // a chest that already holds this item (keeps things together), else the nearest with room
        static Container ChestFor(Vector3 home, Vector3 me, ItemDrop.ItemData it)
        {
            string name = it.m_dropPrefab.name;
            Container best = null; float bd = float.MaxValue;
            List<Container> near = MHR_Chests.Near(home, SearchRadius);
            for (int i = 0; i < near.Count; i++)
            {
                Container c = near[i];
                if (!UsableChest(c) || FindByPrefab(c.GetInventory(), name) == null || !c.GetInventory().CanAddItem(it, it.m_stack)) continue;
                float d = Vector3.Distance(me, c.transform.position);
                if (d < bd && MHR_Chests.CanReach(me, c)) { bd = d; best = c; }
            }
            if (best != null) return best;
            for (int i = 0; i < near.Count; i++)
            {
                Container c = near[i];
                if (!UsableChest(c) || !c.GetInventory().CanAddItem(it, it.m_stack)) continue;
                float d = Vector3.Distance(me, c.transform.position);
                if (d < bd && MHR_Chests.CanReach(me, c)) { bd = d; best = c; }
            }
            return best;
        }

        // at the chest: the item it came for, plus anything else that already has a stack in this chest
        static int Deposit(CompanionSetup cs, State st, Inventory inv, Inventory cinv)
        {
            int mode = cs.GetComponent<ZNetView>() != null && cs.GetComponent<ZNetView>().GetZDO() != null ? cs.GetComponent<ZNetView>().GetZDO().GetInt(CompanionSetup.ActionModeHash, 0) : 0;
            List<ItemDrop.ItemData> items = UnloadList(cs, st, inv, mode);
            int moved = 0;
            for (int i = 0; i < items.Count; i++)
            {
                ItemDrop.ItemData it = items[i];
                string name = it.m_dropPrefab.name;
                if (name != st.Prefab && FindByPrefab(cinv, name) == null) continue;
                if (!cinv.CanAddItem(it, it.m_stack)) continue;
                cinv.MoveItemToThis(inv, it);
                if (!FindSame(inv, it)) { moved++; LogDebug("  Put away \"" + name + "\" x" + it.m_stack); }
            }
            return moved;
        }

        // ---- job -> needed kinds ----
        static int[] KindsFor(int mode)
        {
            switch (mode)
            {
                case 1: return new[] { K_AXE };
                case 2: case 3: return new[] { K_PICK };
                case 7: return new[] { K_BOW, K_ARROW };
                case 8: return new[] { K_CULT };
                case 9: return new[] { K_ROD, K_BAIT, K_FOOD };   // 1.4.111: stamina food for reeling (user)
                case 15: return new[] { K_HAMMER };
                default: return null;
            }
        }
        // the tools (not ammo, bait or food) the job uses that are in the pack
        static HashSet<int> OwnedKinds(Inventory inv, int mode)
        {
            HashSet<int> set = new HashSet<int>();
            int[] k = KindsFor(mode);
            if (k == null || inv == null) return set;
            for (int i = 0; i < k.Length; i++)
                if (k[i] != K_ARROW && k[i] != K_BAIT && k[i] != K_FOOD && BestInInventory(inv, k[i], null) != null) set.Add(k[i]);
            return set;
        }
        static bool NeedsKind(int mode, int kind)
        {
            int[] k = KindsFor(mode);
            if (k == null) return false;
            for (int i = 0; i < k.Length; i++) if (k[i] == kind) return true;
            return false;
        }
        static string KindName(int kind)
        {
            switch (kind)
            {
                case K_AXE: return "an axe"; case K_PICK: return "a pickaxe"; case K_BOW: return "a bow"; case K_ARROW: return "arrows";
                case K_CULT: return "a cultivator"; case K_ROD: return "a fishing rod"; case K_BAIT: return "bait"; case K_HAMMER: return "a hammer"; case K_FOOD: return "stamina food";
            }
            return "a tool";
        }
        static void Speak(CompanionSetup cs, int kind)
        {
            string key = null;
            switch (kind)
            {
                case K_AXE: key = "hc_speech_need_axe"; break;
                case K_PICK: key = "hc_speech_need_pickaxe"; break;
                case K_ARROW: key = "hc_speech_need_arrows"; break;
                // 1.5.42: every other tool too (bow, rod, bait, cultivator, hammer, food) - the comment that "the job's own
                // lines cover them" was only true for some
                default:
                    {
                        string line = ModLocalization.LocFmt("hc_why_need_tool", KindName(kind));
                        CompanionStatus.Why(cs, "hc_why_need_tool", false, KindName(kind));
                        cs.GetComponent<CompanionTalk>()?.SayProblem("tool:" + kind, line);
                        return;
                    }
            }
            CompanionTalk t = cs.GetComponent<CompanionTalk>();
            if (t != null) t.SayProblem("tool:" + kind, ModLocalization.Loc(key));
        }

        // ---- item matching ----
        static bool IsKind(ItemDrop.ItemData it, int kind, ItemDrop.ItemData launcher)
        {
            if (it == null || it.m_shared == null) return false;
            ItemDrop.ItemData.SharedData s = it.m_shared;
            switch (kind)
            {
                case K_AXE: return s.m_damages.m_chop > 0f && IsWeapon(s) && s.m_buildPieces == null;
                case K_PICK: return s.m_damages.m_pickaxe > 0f && IsWeapon(s) && s.m_buildPieces == null;
                case K_BOW: return s.m_itemType == ItemDrop.ItemData.ItemType.Bow && s.m_animationState != ItemDrop.ItemData.AnimationState.FishingRod;
                case K_ROD: return s.m_animationState == ItemDrop.ItemData.AnimationState.FishingRod;
                case K_ARROW: case K_BAIT:
                    return s.m_itemType == ItemDrop.ItemData.ItemType.Ammo && launcher != null &&
                           !string.IsNullOrEmpty(s.m_ammoType) && s.m_ammoType == launcher.m_shared.m_ammoType;
                case K_CULT: return HasPlantPiece(s.m_buildPieces);
                case K_HAMMER: return s.m_buildPieces != null && s.m_buildPieces.name != null && s.m_buildPieces.name.IndexOf("Hammer", StringComparison.OrdinalIgnoreCase) >= 0;
                // 1.4.111: food that's mostly stamina (not mead - meads have no food values)
                case K_FOOD: return s.m_itemType == ItemDrop.ItemData.ItemType.Consumable && s.m_foodStamina > 0f && s.m_foodStamina >= s.m_food;
            }
            return false;
        }
        static bool IsWeapon(ItemDrop.ItemData.SharedData s)
        {
            return s.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon || s.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon ||
                   s.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft || s.m_itemType == ItemDrop.ItemData.ItemType.Tool;
        }
        static bool HasPlantPiece(PieceTable pt)
        {
            if (pt == null || pt.m_pieces == null) return false;
            for (int i = 0; i < pt.m_pieces.Count; i++)
                if (pt.m_pieces[i] != null && pt.m_pieces[i].GetComponent<Plant>() != null) return true;
            return false;
        }
        static float Score(ItemDrop.ItemData it, int kind)
        {
            if (kind == K_ARROW || kind == K_BAIT) return it.m_stack;
            if (kind == K_FOOD) return it.m_shared.m_foodStamina;
            ItemDrop.ItemData.SharedData s = it.m_shared;
            float dmg = kind == K_AXE ? s.m_damages.m_chop : kind == K_PICK ? s.m_damages.m_pickaxe : 0f;
            return s.m_toolTier * 10000f + it.m_quality * 1000f + dmg + (it.m_durability / Mathf.Max(1f, s.m_maxDurability));
        }
        static ItemDrop.ItemData BestIn(Inventory inv, int kind, ItemDrop.ItemData launcher)
        {
            ItemDrop.ItemData best = null; float bs = float.MinValue;
            List<ItemDrop.ItemData> items = inv.GetAllItems();
            for (int i = 0; i < items.Count; i++)
            {
                ItemDrop.ItemData it = items[i];
                if (!IsKind(it, kind, launcher)) continue;
                if (it.m_shared.m_useDurability && it.m_durability <= 0f) continue;
                float sc = Score(it, kind);
                if (sc > bs) { bs = sc; best = it; }
            }
            return best;
        }
        static ItemDrop.ItemData BestInInventory(Inventory inv, int kind, ItemDrop.ItemData launcher) { return BestIn(inv, kind, launcher); }

        // 1.5.44: the stamina food it carries for fishing, for the log ("Honey x5"), or null (user: "Did the NPC grab high
        // stamina food automatically for fishing?")
        internal static string StaminaFoodText(Inventory inv)
        {
            ItemDrop.ItemData f = inv != null ? BestIn(inv, K_FOOD, null) : null;
            return f == null ? null : (MHR_List.Local(f.m_shared.m_name) ?? f.m_shared.m_name) + " x" + f.m_stack + " (" + f.m_shared.m_foodStamina.ToString("F0") + " stamina)";
        }
        static ItemDrop.ItemData FindByPrefab(Inventory inv, string prefab)
        {
            if (string.IsNullOrEmpty(prefab)) return null;
            List<ItemDrop.ItemData> items = inv.GetAllItems();
            for (int i = 0; i < items.Count; i++)
                if (items[i] != null && items[i].m_dropPrefab != null && items[i].m_dropPrefab.name == prefab) return items[i];
            return null;
        }
        static bool FindSame(Inventory inv, ItemDrop.ItemData it) { return inv.GetAllItems().Contains(it); }
        static bool RoomFor(Inventory inv, ItemDrop.ItemData it, int kind)
        {
            if (kind == K_FOOD) return inv.CanAddItem(it, 1);
            return (kind == K_ARROW || kind == K_BAIT) ? inv.CanAddItem(it, it.m_stack) : inv.HaveEmptySlot();
        }

        // ---- chests ----
        // 1.4.152: the companion the current ToolKeeper tick is for (its player decides private chests)
        static Component s_current;

        internal static bool UsableChest(Container c, Component who = null)
        {
            return MHR_Chests.Usable(c, who ?? s_current);
        }
        static Container FindBestInChests(Vector3 center, Vector3 me, int kind, ItemDrop.ItemData launcher, out ItemDrop.ItemData found)
        {
            found = null;
            List<KeyValuePair<float, Container>> cands = new List<KeyValuePair<float, Container>>();
            List<Container> near = MHR_Chests.Near(center, SearchRadius);
            for (int i = 0; i < near.Count; i++)
            {
                Container c = near[i];
                if (!UsableChest(c)) continue;
                ItemDrop.ItemData it = BestIn(c.GetInventory(), kind, launcher);
                if (it == null) continue;
                cands.Add(new KeyValuePair<float, Container>(Score(it, kind) - Vector3.Distance(center, c.transform.position) * 0.01f, c));
            }
            if (cands.Count == 0) return null;
            cands.Sort((x, y) => y.Key.CompareTo(x.Key));
            // 1.4.66: best chest he can actually get to
            for (int i = 0; i < cands.Count; i++)
            {
                Container c = cands[i].Value;
                if (MHR_Chests.CanReach(me, c)) { found = BestIn(c.GetInventory(), kind, launcher); return c; }
                Log("Can't reach the chest at " + MHR_Chests.Fmt(c.transform.position) + " (no clear spot next to it) - trying another");
            }
            Container first = cands[0].Value;
            found = BestIn(first.GetInventory(), kind, launcher);
            Log("No chest with " + KindName(kind) + " looks reachable - trying the best one anyway");
            return first;
        }
        static Container FindChestByPos(Vector3 pos, ItemDrop.ItemData it)
        {
            List<Container> near = MHR_Chests.Near(pos, 1.5f);
            for (int i = 0; i < near.Count; i++)
            {
                Container c = near[i];
                if (UsableChest(c) && c.GetInventory().CanAddItem(it, it.m_stack)) return c;
            }
            return null;
        }
        static Container FindChestWithRoom(Vector3 center, ItemDrop.ItemData it)
        {
            Container best = null, bestAny = null; float bd = float.MaxValue, bdAny = float.MaxValue;
            List<Container> near = MHR_Chests.Near(center, SearchRadius);
            for (int i = 0; i < near.Count; i++)
            {
                Container c = near[i];
                if (!UsableChest(c) || !c.GetInventory().CanAddItem(it, it.m_stack)) continue;
                float d = Vector3.Distance(center, c.transform.position);
                if (d < bdAny) { bdAny = d; bestAny = c; }
                if (d < bd && MHR_Chests.CanReach(center, c)) { bd = d; best = c; }
            }
            return best != null ? best : bestAny;
        }

        // ---- persistence (survives logout): "kind|prefab|count|x|y|z;..." ----
        static void Load(State st, ZDO zdo)
        {
            st.Borrowed.Clear();
            string s = zdo.GetString(BorrowHash, "");
            if (string.IsNullOrEmpty(s)) return;
            string[] parts = s.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                string[] f = parts[i].Split('|');
                if (f.Length < 6) continue;
                try
                {
                    st.Borrowed.Add(new Entry
                    {
                        Kind = int.Parse(f[0]), Prefab = f[1], Count = int.Parse(f[2]),
                        Chest = new Vector3(float.Parse(f[3], System.Globalization.CultureInfo.InvariantCulture),
                                            float.Parse(f[4], System.Globalization.CultureInfo.InvariantCulture),
                                            float.Parse(f[5], System.Globalization.CultureInfo.InvariantCulture))
                    });
                }
                catch { }
            }
        }
        static void Save(State st, ZDO zdo)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < st.Borrowed.Count; i++)
            {
                Entry e = st.Borrowed[i];
                if (sb.Length > 0) sb.Append(';');
                sb.Append(e.Kind).Append('|').Append(e.Prefab).Append('|').Append(e.Count).Append('|')
                  .Append(e.Chest.x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)).Append('|')
                  .Append(e.Chest.y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)).Append('|')
                  .Append(e.Chest.z.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
            }
            zdo.Set(BorrowHash, sb.ToString());
        }
        static void LogDebug(string msg) { if (CompanionsPlugin.Log != null) CompanionsPlugin.Log.LogDebug("[Tools] " + msg); }
        static void Log(string msg) { if (CompanionsPlugin.Log != null) CompanionsPlugin.Log.LogInfo("[Tools] " + msg); }
    }

    // ---- 1.4.65: a job and Stay Home (Homestead) can't both be on ----
    public static class MHR_JobRules
    {
        public static bool HasJob(CompanionSetup cs)
        {
            if (cs == null) return false;
            ZNetView nv = cs.GetComponent<ZNetView>();
            ZDO zdo = nv != null ? nv.GetZDO() : null;
            return zdo != null && zdo.GetInt(CompanionSetup.ActionModeHash, 0) != 0;
        }

        // 1.4.75: a work mode is on (not Follow, and not the old Stay mode that pre-1.4.65 saves can still hold).
        // 1.4.145: an order is being carried out (go there, hitch the cart, put things in a chest, fetch the tombstone).
        // CompanionAI walks it with the mover; a job steering at the same time cancels it out every frame, so jobs wait.
        public static bool OrderPending(Component c)
        {
            if (OtherOrder(c)) return true;
            // 1.4.146: a repair the player ordered at a station
            RepairController rc = c != null ? c.GetComponent<RepairController>() : null;
            return rc != null && rc.IsDirect;
        }

        // every order but the ordered repair (which RepairController itself carries out)
        public static bool OtherOrder(Component c)
        {
            if (c == null) return false;
            // Fase 3.1: "recolhe os itens do chão" (MHR_Collect) - a one-shot order the player gave, so the jobs hold
            // still while it walks about, exactly like the orders below (they all drive through the same MHR_Mover,
            // and two tags steering at once reset each other's route every frame)
            if (MHR_Collect.Active(c)) return true;
            CompanionAI ai = c.GetComponent<CompanionAI>();
            if (ai != null && (ai.PendingMoveTarget.HasValue || ai.PendingCartAttach != null || ai.PendingDepositContainer != null || ai.IsRecoveringTombstone || ai.IsBoardingShip)) return true;
            // 1.4.147: open that door; sit by that fire / sleep in that bed
            DoorHandler door = c.GetComponent<DoorHandler>();
            if (door != null && door.IsActive) return true;
            CompanionRest rest = c.GetComponent<CompanionRest>();
            return rest != null && rest.IsDirectedOrder;
        }

        public static bool IsJobMode(int mode)
        {
            return mode != CompanionSetup.ModeFollow && mode != CompanionSetup.ModeStay;
        }

        // 1.4.78: a job and Follow can't both be on either (user, 1.4.77 test): with Follow on, a companion whose job had
        // nothing to do trailed after the player instead of staying at the work. Picking a job turns Follow off; turning
        // the job off turns Follow back on if the job was what turned it off. Picking Follow turns the job off (radial).
        static readonly int FollowPausedHash = "HC_FollowPausedByJob".GetStableHashCode();

        // 1.4.120: Wood, Stone, Ore and Forage work like Hunt (user: "just trigger on targets of opportunity and no longer
        // turn off follow automatically"): with Follow on they work what's close to you and otherwise follow
        public static bool KeepsFollow(int mode)
        {
            return mode == CompanionSetup.ModeHunt || mode == CompanionSetup.ModeGatherWood || mode == CompanionSetup.ModeGatherStone ||
                   mode == CompanionSetup.ModeGatherOre || mode == CompanionSetup.ModeForage;
        }

        // 1.4.150: the player switched Follow on/off themselves - forget that a job had paused it
        public static void FollowSetByPlayer(CompanionSetup cs)
        {
            ZNetView nv = cs != null ? cs.GetComponent<ZNetView>() : null;
            ZDO zdo = nv != null ? nv.GetZDO() : null;
            if (zdo != null && zdo.GetBool(FollowPausedHash)) zdo.Set(FollowPausedHash, false);
        }

        public static void OnJobSet(CompanionSetup cs, int mode)
        {
            if (cs == null) return;
            ZNetView nv = cs.GetComponent<ZNetView>();
            ZDO zdo = nv != null ? nv.GetZDO() : null;
            if (mode != 0)
            {
                // 1.4.94: Hunt works with Follow on (hunts near you) or off (hunts around where it is)
                if (!KeepsFollow(mode) && cs.GetFollow())
                {
                    cs.SetFollow(false);
                    if (zdo != null) zdo.Set(FollowPausedHash, true);
                    if (CompanionsPlugin.Log != null) CompanionsPlugin.Log.LogDebug("[Jobs] Job " + mode + " picked - Follow turned off");
                }
            }
            else if (zdo != null && zdo.GetBool(FollowPausedHash))
            {
                zdo.Set(FollowPausedHash, false);
                cs.SetFollow(true);
                if (CompanionsPlugin.Log != null) CompanionsPlugin.Log.LogDebug("[Jobs] Job turned off - Follow back on");
            }
        }
    }

    // ---- 1.4.66: every chest near a point (no collider cap), and which side of a chest to stand on ----
    public static class MHR_Chests
    {
        // ---- 1.4.152: one set of chest rules for every job (multiplayer-safe) ----

        // a chest a companion may use: a real storage chest (not a cart, ship, tombstone or someone's pack), inside a
        // ward that lets it in, a private chest only if its own player built it, and nobody has it open
        public static bool Usable(Container c, Component who)
        {
            if (c == null || c.GetInventory() == null) return false;
            if (c.GetComponent<Character>() != null || c.GetComponentInParent<Character>() != null) return false;
            if (c.GetComponent<TombStone>() != null) return false;
            if (c.GetComponent<Vagon>() != null || c.GetComponentInParent<Ship>() != null) return false;
            if (c.GetComponent<Piece>() == null) return false;
            // 1.5.70: only chests a player built - not the loot chests of huts, camps and dungeons (user report: "The
            // companion uses neutral chests, so it can stash its loot on the way home")
            if (!c.GetComponent<Piece>().IsPlacedByPlayer()) return false;
            ZNetView nv = c.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return false;
            if (InUse(c)) return false;
            if (!PrivateArea.CheckAccess(c.transform.position, 0f, flash: false)) return false;
            if (c.m_privacy == Container.PrivacySetting.Private)
            {
                Piece pc = c.GetComponent<Piece>();
                if (pc == null || pc.GetCreator() != MHR_Owner.CreatorId(who)) return false;
            }
            return true;
        }

        // someone has it open - here (the local flag) or on another player's game (the synced flag)
        public static bool InUse(Container c)
        {
            if (c == null) return true;
            if (c.IsInUse()) return true;
            ZNetView nv = c.GetComponent<ZNetView>();
            return nv != null && nv.IsValid() && !nv.IsOwner() && nv.GetZDO().GetInt(ZDOVars.s_inUse) == 1;
        }

        // make this game the chest's owner before changing what's in it - the game only saves a chest's contents on its
        // owner, so changes made elsewhere were lost again (or items duplicated) in multiplayer
        public static void Claim(Container c)
        {
            ZNetView nv = c != null ? c.GetComponent<ZNetView>() : null;
            if (nv != null && nv.IsValid() && !nv.IsOwner()) nv.ClaimOwnership();
        }

        // open it like a player: false if someone else has it open; otherwise this game owns it and it shows as in use
        public static bool Open(Container c)
        {
            if (c == null || InUse(c)) return false;
            // 1.5.70: not before you've spawned - the game's SetInUse looks you up for the lid's effects (log c6fee971: an
            // NRE sorting a chest a second after loading in)
            if (Player.m_localPlayer == null) return false;
            ZNetView nv = c.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return false;
            Claim(c);
            c.SetInUse(true);
            return true;
        }

        public static void Close(Container c)
        {
            if (c == null || Player.m_localPlayer == null) return;
            ZNetView nv = c.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return;
            c.SetInUse(false);
        }

        // 1.4.152: near home, the chests around home count too (HomeTripRange) - Smelt and Cook only saw the chests within
        // their own 25 m, found no wood, and took down the piles Repair and Restock had just built from those home chests
        public static void AddHomeChests(List<Container> list, CompanionSetup cs)
        {
            if (list == null || cs == null || !cs.HasHomePosition() || !MHR_ToolKeeper.NearHome(cs)) return;
            foreach (Container c in Near(cs.GetHomePosition(), MHR_ToolKeeper.HomeRange()))
            {
                if (c != null && c.gameObject != cs.gameObject && !list.Contains(c) && Usable(c, cs)) list.Add(c);
            }
        }

        // 1.4.152: the items already lying around before a companion empties a hive or takes down a pile - only what
        // comes out of its own hive/pile is picked up afterwards (it took another companion's honey)
        public static HashSet<int> DropsNear(Vector3 center, float radius)
        {
            HashSet<int> ids = new HashSet<int>();
            foreach (Collider c in Physics.OverlapSphere(center, radius))
            {
                ItemDrop d = c != null ? c.GetComponentInParent<ItemDrop>() : null;
                if (d != null) ids.Add(d.GetInstanceID());
            }
            return ids;
        }

        static readonly List<Piece> s_pieces = new List<Piece>();
        static readonly Collider[] s_small = new Collider[32];
        static readonly List<Vector3> s_path = new List<Vector3>();
        static int s_mask = -1;
        static int Mask()
        {
            if (s_mask == -1) s_mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");
            return s_mask;
        }

        public static List<Container> Near(Vector3 center, float radius)
        {
            List<Container> list = new List<Container>();
            s_pieces.Clear();
            Piece.GetAllPiecesInRadius(center, radius, s_pieces);
            HashSet<int> seen = new HashSet<int>();
            for (int i = 0; i < s_pieces.Count; i++)
            {
                Piece p = s_pieces[i];
                if (p == null) continue;
                Container c = p.GetComponentInChildren<Container>();
                if (c == null || !seen.Add(c.GetInstanceID())) continue;
                list.Add(c);
            }
            return list;
        }

        public static List<CraftingStation> Stations(Vector3 center, float radius)
        {
            List<CraftingStation> list = new List<CraftingStation>();
            s_pieces.Clear();
            Piece.GetAllPiecesInRadius(center, radius, s_pieces);
            HashSet<int> seen = new HashSet<int>();
            for (int i = 0; i < s_pieces.Count; i++)
            {
                Piece p = s_pieces[i];
                if (p == null) continue;
                CraftingStation c = p.GetComponentInChildren<CraftingStation>();
                if (c == null || !seen.Add(c.GetInstanceID())) continue;
                list.Add(c);
            }
            return list;
        }

        static bool IsChestPart(Collider col, Container chest)
        {
            return col != null && chest != null && col.GetComponentInParent<Container>() == chest;
        }

        // nothing solid between someone standing at 'from' and the chest
        // 1.4.88: reach like a player - Valheim lets you use something within 5 m of your eyes that you can see. Companions
        // get 4 m from eye height (1.6 m) to the middle of the chest, with a clear line from the eyes or the chest
        // (user: "I can open all the chests without issue" - the ones on the ledge 2 m up included).
        public const float EyeHeight = 1.6f, EyeReach = 4f;
        public static bool InReach(Vector3 feet, Container chest)
        {
            if (chest == null) return false;
            Vector3 eye = feet + Vector3.up * EyeHeight;
            return Vector3.Distance(eye, chest.transform.position + Vector3.up * 0.5f) <= EyeReach && LineClear(feet, chest);
        }

        public static bool LineClear(Vector3 from, Container chest)
        {
            // eyes to the middle, a little higher to the top (a player's camera sits above and behind them), waist height
            return LineClear(from, chest, EyeHeight, 0.5f) || LineClear(from, chest, 1.9f, 0.9f) || LineClear(from, chest, 1.0f, 0.5f);
        }

        static bool LineClear(Vector3 from, Container chest, float height, float target)
        {
            if (chest == null) return false;
            Vector3 a = from + Vector3.up * height;
            Vector3 b = chest.transform.position + Vector3.up * target;
            Vector3 d = b - a; float len = d.magnitude;
            if (len < 0.05f) return true;
            RaycastHit[] hits = Physics.RaycastAll(a, d / len, len, Mask());
            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null || IsChestPart(col, chest)) continue;
                if (col.GetComponentInParent<Character>() != null) continue;
                // 1.4.67: only walls, floors and other big solid things block; other chests, tables and clutter don't
                if (col.GetComponentInParent<Container>() != null) continue;
                Vector3 size = col.bounds.size;
                if (Mathf.Max(size.x, Mathf.Max(size.y, size.z)) < 1.2f) continue;
                return false;
            }
            return true;
        }

        static readonly Dictionary<int, KeyValuePair<float, bool>> s_reach = new Dictionary<int, KeyValuePair<float, bool>>();
        public static bool CanReach(Vector3 me, Container chest)
        {
            if (chest == null) return false;
            if (InReach(me, chest)) return true;
            int id = chest.GetInstanceID();
            KeyValuePair<float, bool> c;
            if (s_reach.TryGetValue(id, out c) && Time.time - c.Key < 30f) return c.Value;
            Vector3 spot;
            bool ok = PickApproach(me, chest, out spot);
            if (s_reach.Count > 500) s_reach.Clear();
            s_reach[id] = new KeyValuePair<float, bool>(Time.time, ok);
            return ok;
        }
        public static string Fmt(Vector3 v) { return "(" + v.x.ToString("F1") + ", " + v.y.ToString("F1") + ", " + v.z.ToString("F1") + ")"; }

        public static bool PickApproach(Vector3 me, Container chest, out Vector3 spot)
        {
            spot = Vector3.zero;
            if (chest == null) return false;
            Vector3 cp = chest.transform.position;
            float best = float.MaxValue; bool found = false;
            List<Vector3> viaDoor = null;
            // 1.4.85: out to 2.2 m (inside the 2-2.5 m use distance) and every 22.5 degrees, for chests in narrow spots
            // 1.4.88: also lower down (up to 2.6 m below, e.g. the floor under a ledge) and further out, as long as it's in
            // reach from eye height like a player (InReach)
            float[] radii = { 1.2f, 1.7f, 2.2f, 2.8f };
            int nGround = 0, nHeight = 0, nFire = 0, nBlocked = 0, nReach = 0, nRoute = 0;
            for (int r = 0; r < radii.Length; r++)
            {
                for (int k = 0; k < 16; k++)
                {
                    float ang = k * 22.5f * Mathf.Deg2Rad;
                    Vector3 pt = cp + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radii[r];
                    // 1.4.89: every surface straight down, not just the first - next to a ledge chest the first one is the
                    // ledge itself and the floor below (where a player stands to open it) was never looked at
                    RaycastHit[] hits = Physics.RaycastAll(pt + Vector3.up * 1.6f, Vector3.down, 4.5f, Mask());
                    if (hits.Length == 0) { nGround++; continue; }
                    System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                    for (int h = 0; h < hits.Length; h++)
                    {
                        RaycastHit hit = hits[h];
                        if (hit.normal.y < 0.6f || IsChestPart(hit.collider, chest) || IsFurniture(hit.collider)) continue;
                        Vector3 stand = hit.point;
                        if (stand.y > cp.y + 1.2f || stand.y < cp.y - 2.6f) { nHeight++; continue; }
                        if (MHR_Mover.InFire(stand)) { nFire++; continue; }   // 1.4.88
                        if (Blocked(stand + Vector3.up * 1.0f, chest)) { nBlocked++; continue; }
                        if (!InReach(stand, chest)) { nReach++; continue; }
                        s_path.Clear();
                        if (Pathfinding.instance == null || !Pathfinding.instance.GetPath(me, stand, s_path, MHR_CompanionNavMesh.AgentOrHumanoid, false, true, true) || s_path.Count == 0)
                        { nRoute++; if (viaDoor == null) viaDoor = new List<Vector3>(); viaDoor.Add(stand); continue; }
                        // 1.4.67: the route has to actually end at the spot, not just somewhere near it
                        Vector3 end = s_path[s_path.Count - 1];
                        if (new Vector2(end.x - stand.x, end.z - stand.z).magnitude > 0.6f || Mathf.Abs(end.y - stand.y) > 1.0f)
                        { nRoute++; if (viaDoor == null) viaDoor = new List<Vector3>(); viaDoor.Add(stand); continue; }
                        float score = 0f;
                        Vector3 prev = me;
                        for (int i = 0; i < s_path.Count; i++) { score += Vector3.Distance(prev, s_path[i]); prev = s_path[i]; }
                        if (score < best) { best = score; spot = stand; found = true; }
                    }
                }
                if (found) break;
            }
            // 1.4.84: no route straight there, but maybe with a hop or through closed doors (routes don't use some stairs,
            // and closed doors block them) - 1.4.83 test: the chests in the house up the stairs above the farm were all
            // "can't get within reach" from the farm, so crops never got stored
            if (!found && viaDoor != null)
            {
                for (int i = 0; i < viaDoor.Count && i < 4; i++)
                {
                    if (MHR_Mover.ReachableViaDoor(me, viaDoor[i])) { spot = viaDoor[i]; found = true; break; }
                }
            }
            // 1.4.89: why no spot worked, so a chest the player can open but the companion can't shows the reason
            if (!found && CompanionsPlugin.Log != null) CompanionsPlugin.Log.LogDebug("[Chests] No spot to stand by the chest at " + Fmt(cp) + ": " + nGround + " no ground, " + nHeight + " too high/low, " + nFire + " in a fire, " + nBlocked + " no room, " + nReach + " out of reach/sight, " + nRoute + " no route");
            return found;
        }

        // 1.4.78: the same for any building piece (workbench, forge...): a spot in reach of it that a route actually ends
        // at, instead of its centre (which is inside it, so routes never reach it - 1.4.77 log: swam across instead of
        // taking the door).
        public static bool PickApproachPiece(Vector3 me, Component target, out Vector3 spot)
        {
            spot = Vector3.zero;
            if (target == null) return false;
            return PickNear(me, target.transform.position, target.GetComponentInParent<Piece>(), s_pieceRadii, 8, 1.5f, out spot);
        }
        static readonly float[] s_pieceRadii = { 1.3f, 1.8f, 2.2f };   // within the 2.5 m repair reach
        static readonly float[] s_pointRadii = { 0.9f, 1.4f, 1.9f };

        // 1.4.86: the same around one point of a piece, e.g. a smelter's ore or fuel slot
        public static bool PickApproachPoint(Vector3 me, Vector3 point, Component owner, out Vector3 spot)
        {
            return PickNear(me, point, owner != null ? owner.GetComponentInParent<Piece>() : null, s_pointRadii, 12, 1.8f, out spot);
        }

        static bool PickNear(Vector3 me, Vector3 cp, Piece piece, float[] radii, int dirs, float maxDy, out Vector3 spot)
        {
            spot = Vector3.zero;
            float best = float.MaxValue; bool found = false;
            for (int r = 0; r < radii.Length; r++)
            {
                for (int k = 0; k < dirs; k++)
                {
                    float ang = k * (360f / dirs) * Mathf.Deg2Rad;
                    Vector3 pt = cp + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radii[r];
                    // 1.4.91: every surface straight down, skipping furniture tops (1.4.90 log: the stand spot for a
                    // fermenter was on top of the one next to it, 2.2 m up)
                    RaycastHit[] hits = Physics.RaycastAll(pt + Vector3.up * 1.6f, Vector3.down, 3.2f + maxDy, Mask());
                    System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                    for (int h = 0; h < hits.Length; h++)
                    {
                        RaycastHit hit = hits[h];
                        if (IsPart(hit.collider, piece) || hit.normal.y < 0.6f || IsFurniture(hit.collider)) continue;
                        Vector3 stand = hit.point;
                        if (Mathf.Abs(stand.y - cp.y) > maxDy) continue;
                        if (MHR_Mover.InFire(stand)) continue;   // 1.4.88: not in the fire under the cauldron
                        if (BlockedNear(stand + Vector3.up * 1.0f, piece)) continue;
                        if (!ClearTo(stand, cp, piece)) continue;
                        s_path.Clear();
                        if (Pathfinding.instance == null || !Pathfinding.instance.GetPath(me, stand, s_path, MHR_CompanionNavMesh.AgentOrHumanoid, false, true, true)) continue;
                        if (s_path.Count == 0) continue;
                        Vector3 end = s_path[s_path.Count - 1];
                        if (new Vector2(end.x - stand.x, end.z - stand.z).magnitude > 0.6f || Mathf.Abs(end.y - stand.y) > 1.0f) continue;
                        float score = 0f;
                        Vector3 prev = me;
                        for (int i = 0; i < s_path.Count; i++) { score += Vector3.Distance(prev, s_path[i]); prev = s_path[i]; }
                        if (score < best) { best = score; spot = stand; found = true; }
                        break;   // the first walkable surface from the top is the one to stand on
                    }
                }
                if (found) break;
            }
            return found;
        }
        static bool IsPart(Collider col, Piece piece) { return col != null && piece != null && col.GetComponentInParent<Piece>() == piece; }
        // 1.4.91: the top of a chest, fermenter, station, smelter, bed or chair isn't somewhere to stand
        public static bool IsFurniture(Collider col)
        {
            if (col == null) return false;
            return col.GetComponentInParent<Container>() != null || col.GetComponentInParent<Fermenter>() != null
                || col.GetComponentInParent<CraftingStation>() != null || col.GetComponentInParent<CookingStation>() != null
                || col.GetComponentInParent<Smelter>() != null || col.GetComponentInParent<Bed>() != null
                || col.GetComponentInParent<Chair>() != null;
        }
        static bool BlockedNear(Vector3 p, Piece piece)
        {
            int n = Physics.OverlapSphereNonAlloc(p, 0.35f, s_small, Mask());
            for (int i = 0; i < n; i++)
            {
                Collider col = s_small[i];
                if (col == null || IsPart(col, piece) || col.GetComponentInParent<Character>() != null) continue;
                return true;
            }
            return false;
        }
        static bool ClearTo(Vector3 from, Vector3 to, Piece piece)
        {
            Vector3 a = from + Vector3.up * 1.0f, b = to + Vector3.up * 0.5f;
            Vector3 d = b - a; float len = d.magnitude;
            if (len < 0.05f) return true;
            RaycastHit[] hits = Physics.RaycastAll(a, d / len, len, Mask());
            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null || IsPart(col, piece) || col.GetComponentInParent<Character>() != null) continue;
                Vector3 size = col.bounds.size;
                if (Mathf.Max(size.x, Mathf.Max(size.y, size.z)) < 1.2f) continue;
                return false;
            }
            return true;
        }

        static bool Blocked(Vector3 p, Container chest)
        {
            int n = Physics.OverlapSphereNonAlloc(p, 0.35f, s_small, Mask());
            for (int i = 0; i < n; i++)
            {
                Collider col = s_small[i];
                if (col == null || IsChestPart(col, chest)) continue;
                if (col.GetComponentInParent<Character>() != null) continue;
                return true;
            }
            return false;
        }
    }

    // ---- 1.4.66: movement with a progress log and a forward jump when stuck ----
    public static class MHR_Nav
    {
        public static bool MoveFarm(CompanionAI ai, float dt, Vector3 point, float dist, bool run) { return Move(ai, dt, point, dist, run, "Farm"); }
        public static bool MovePlant(CompanionAI ai, float dt, Vector3 point, float dist, bool run) { return Move(ai, dt, point, dist, run, "Plant"); }
        public static bool MoveSmelt(CompanionAI ai, float dt, Vector3 point, float dist, bool run) { return Move(ai, dt, point, dist, run, "Smelt"); }

        // 1.4.70: "in range" the way a player reaches things: flat distance while within 2.2m up or down (stacked chests,
        // crops below a low edge); real distance beyond that
        // 1.4.71: doors are opened from anywhere in reach, including from the bottom of the stairs below them
        public static float DoorDist(Vector3 a, Vector3 b)
        {
            float dy = Mathf.Abs(a.y - b.y);
            return dy <= 3.0f ? new Vector2(a.x - b.x, a.z - b.z).magnitude : Vector3.Distance(a, b);
        }
        // 1.4.71: near home, gear gets repaired at any wear (away from home the Repair > DurabilityThreshold setting applies)
        public static float RepairThreshold(Component c)
        {
            float cfg = ModConfig.RepairDurabilityThreshold != null ? ModConfig.RepairDurabilityThreshold.Value : 0.5f;
            if (cfg > 1f) cfg /= 100f;
            CompanionSetup cs = c != null ? c.GetComponent<CompanionSetup>() : null;
            // 1.4.78: only actually at home (within 25 m of the home spot, or HomeTripRange if smaller). With the whole
            // HomeTripRange (60 m) he set off to repair a 99% sword while out with the player (1.4.77 test).
            // 1.4.133: [Repair] HomeThreshold (80%) - at any wear it went off to the workbench every time the hammer dropped to
            // 99% during Repair and Restock (user: "the NPC seems to repair a lot")
            // 1.5.70: or in a base (a workbench's base area) without a home set (log 31cb468a: Gunter, no home, at the forge with
            // gear at 80-94% - user: "Can you check if the NPC should have fixed up their gear when we were home?")
            bool inBase = false;
            if (cs != null && !cs.HasHomePosition())
            {
                try { inBase = EffectArea.IsPointInsideArea(cs.transform.position, EffectArea.Type.PlayerBase, 0f) != null; } catch { }
            }
            if (cs != null && ((cs.HasHomePosition() && MHR_ToolKeeper.AtHome(cs)) || inBase)) return ModConfig.RepairHomeThreshold != null ? Mathf.Max(cfg, ModConfig.RepairHomeThreshold.Value) : 0.8f;
            return cfg;
        }

        // 1.4.73: turn toward a target at a player-like rate instead of snapping
        public static void TurnTo(Transform t, Quaternion q)
        {
            if (t == null) return;
            t.rotation = Quaternion.RotateTowards(t.rotation, q, 540f * Time.deltaTime);
        }

        // 1.4.74: RepairController's automatic repair only looked 20m around the companion. Near home, any station near
        // home counts (HomeTripRange); the distance is scaled down so the nearest still wins.
        public static float StationScanDist(Vector3 me, Vector3 station, Component c)
        {
            float d = Vector3.Distance(me, station);
            try
            {
                CompanionSetup cs = c != null ? c.GetComponent<CompanionSetup>() : null;
                if (cs != null && cs.HasHomePosition() && MHR_ToolKeeper.NearHome(cs) &&
                    Vector3.Distance(cs.GetHomePosition(), station) <= MHR_ToolKeeper.HomeRange()) return d * 0.01f;
            }
            catch { }
            return d;
        }

        public static float ReachDist(Vector3 a, Vector3 b)
        {
            float dy = Mathf.Abs(a.y - b.y);
            return dy <= 2.2f ? new Vector2(a.x - b.x, a.z - b.z).magnitude : Vector3.Distance(a, b);
        }

        // 1.4.69: jobs walk; they only run when the target is further than WorkRunDistance (0 = never run for work)
        public static bool FarEnoughToRun(CompanionAI ai, Vector3 point)
        {
            float d = ModConfig.WorkRunDistance != null ? ModConfig.WorkRunDistance.Value : 25f;
            return d > 0f && Vector3.Distance(ai.transform.position, point) > d;
        }
        // 1.4.134 (movement phase 4): jobs give a leg up when the movement does, or after this long whatever happens
        public const float LegTimeout = 120f;

        public static bool Move(CompanionAI ai, float dt, Vector3 point, float dist, bool run, string tag)
        {
            // 1.4.68: planting needs him on the field itself, not standing above or below it (deck over the field).
            // (1.4.70: chests and crops are reached from wherever they're in range, like a player - the callers check that.)
            Vector3 d = point - ai.transform.position;
            float flat = new Vector2(d.x, d.z).magnitude;
            float useDist = dist;
            if (tag == "Plant" && Mathf.Abs(d.y) > 1.0f && flat <= dist) useDist = Mathf.Min(dist, 0.5f);
            // 1.4.77 (movement phase 3): through MHR_Mover - routes, doors, water, the shared stuck ladder.
            // It walks the last few steps, jogs in between and runs past WorkRunDistance when 'run' is allowed.
            return MHR_Mover.Go(ai, dt, point, useDist, run, tag);
        }

    }

    // ---- 1.4.70: sprint in bursts. Run until stamina is down to half (30% in a fight), then walk until it's back to 90% ----
    public static class MHR_Sprint
    {
        static readonly Dictionary<int, bool> s_isCompanion = new Dictionary<int, bool>();
        static readonly Dictionary<int, bool> s_resting = new Dictionary<int, bool>();

        public static void Filter(Character c, ref bool run)
        {
            if (!run || c == null) return;
            try
            {
                int id = c.GetInstanceID();
                bool comp;
                if (!s_isCompanion.TryGetValue(id, out comp))
                {
                    comp = c.GetComponent<CompanionSetup>() != null;
                    if (s_isCompanion.Count > 2000) s_isCompanion.Clear();
                    s_isCompanion[id] = comp;
                }
                if (!comp) return;
                CompanionStamina st = c.GetComponent<CompanionStamina>();
                if (st == null || st.MaxStamina <= 0f) return;
                float pct = st.Stamina / st.MaxStamina;
                CompanionAI ai = c.GetComponent<CompanionAI>();
                bool fight = ai != null && ai.IsInCombat;
                bool resting;
                s_resting.TryGetValue(id, out resting);
                if (resting)
                {
                    if (pct >= 0.9f) { s_resting[id] = false; return; }
                    if (fight && pct > 0.3f) return;
                    run = false;
                    return;
                }
                if (pct <= (fight ? 0.3f : 0.5f)) { s_resting[id] = true; run = false; }
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(Character), "SetRun")]
    public static class MHR_SprintBurst_Patch
    {
        static void Prefix(Character __instance, ref bool run) { MHR_Sprint.Filter(__instance, ref run); }
    }

    // ---- 1.4.72: when the companion's inventory is reloaded (ownership moves between machines or it reloads from its
    // save data), equipped items are rebuilt as new objects. The old check unequipped anything "missing" - which stripped
    // all armor, and with a job holding auto-equip off it stayed off. Swap to the matching new item instead. ----
    public static class MHR_Equip
    {
        // 1.4.73: which item a swing wears down: the right-hand weapon/tool, or a bow in the left hand. Never a shield
        // or torch just because the right hand was empty.
        public static ItemDrop.ItemData DurabilityItem(Humanoid h)
        {
            ItemDrop.ItemData r = h != null ? h.GetCurrentWeapon() : null;
            return (r != null && h.IsItemEquiped(r)) ? r : null;
        }

        // 1.4.73: a broken tool comes off and the companion goes back to its normal gear (best weapon, shield)
        public static void OnToolBroke(Humanoid h)
        {
            try
            {
                if (h == null) return;
                HarvestController hc = h.GetComponent<HarvestController>();
                if (hc != null) hc.RestoreLoadout();
                CompanionSetup cs = h.GetComponent<CompanionSetup>();
                if (cs != null) { cs.SuppressAutoEquip = false; cs.SyncEquipmentToInventory(); }
            }
            catch { }
        }

        // 1.4.73: chopping with a one-handed tool keeps the shield (or torch) on, like a player
        public static void ClearLeftFor(Humanoid h, ItemDrop.ItemData left, ItemDrop.ItemData tool)
        {
            if (h == null || left == null) return;
            bool twoHanded = tool != null && tool.m_shared != null &&
                (tool.m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon || tool.m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft ||
                 tool.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow);
            bool keepable = left.m_shared != null && (left.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield || left.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch);
            if (!twoHanded && keepable) return;
            h.UnequipItem(left, false);
        }

        public static void SwapOrUnequip(Humanoid h, List<ItemDrop.ItemData> items, ItemDrop.ItemData old)
        {
            if (h == null || old == null) return;
            ItemDrop.ItemData match = null;
            try
            {
                string name = old.m_dropPrefab != null ? old.m_dropPrefab.name : null;
                if (name != null && items != null)
                    for (int i = 0; i < items.Count; i++)
                    {
                        ItemDrop.ItemData it = items[i];
                        if (it == null || it == old || it.m_equipped || it.m_dropPrefab == null) continue;
                        if (it.m_dropPrefab.name == name && it.m_quality == old.m_quality && it.m_variant == old.m_variant) { match = it; break; }
                    }
            }
            catch { match = null; }
            h.UnequipItem(old, false);
            if (match != null && !(match.m_shared.m_useDurability && match.m_durability <= 0f))
            {
                h.EquipItem(match, false);
                if (CompanionsPlugin.Log != null) CompanionsPlugin.Log.LogDebug("[Setup] Inventory reloaded - kept \"" + match.m_shared.m_name + "\" equipped");
            }
        }
    }
}
