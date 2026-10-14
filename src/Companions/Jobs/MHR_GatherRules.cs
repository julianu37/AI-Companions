using System;
using System.Collections.Generic;
using UnityEngine;

namespace Companions
{
    // ---- 1.4.64: what the gather jobs may take, how much, and staying safe around falling trees ----
    public static class MHR_GatherRules
    {
        class Per
        {
            public int LimitRejects, OtherRejects;
            // Fase 3.2: candidates this frame's scan turned away because an order's ceiling was met (see CapReject)
            public int CapRejects; public int CapFrame;
            public float NextPlenty;
            public bool PackFull;
            public float DodgeUntil; public Vector3 DodgePoint;
            public int ApproachFor; public Vector3 ApproachOffset; public bool ApproachHas;
            public bool TooHeavy; public float LastHeavyCall, NextDropOffCheck, NextHeavySpeak;
            public bool PileActive; public float PileUntil; public Vector3 PilePoint; public bool PileHas; public Vector3 PileSpot;
        }
        static readonly Dictionary<int, Per> s_per = new Dictionary<int, Per>();
        static readonly List<Piece> s_pieces = new List<Piece>();
        static readonly Dictionary<string, float> s_countTime = new Dictionary<string, float>();
        static readonly Dictionary<string, int> s_countVal = new Dictionary<string, int>();
        static readonly Dictionary<int, KeyValuePair<float, bool>> s_protect = new Dictionary<int, KeyValuePair<float, bool>>();
        static readonly Dictionary<int, List<string>> s_dropsCache = new Dictionary<int, List<string>>();
        static readonly Dictionary<string, Dictionary<string, int>> s_listCache = new Dictionary<string, Dictionary<string, int>>();
        static readonly HashSet<string> s_checkedNames = new HashSet<string>();

        static Per P(Component c)
        {
            int id = c.GetInstanceID(); Per p;
            if (!s_per.TryGetValue(id, out p)) { p = new Per(); s_per[id] = p; }
            return p;
        }

        // ---------- lists ----------
        static string ListFor(int mode)
        {
            switch (mode)
            {
                case 1: return ModConfig.GatherWood != null ? ModConfig.GatherWood.Value : null;
                case 2: return ModConfig.GatherStone != null ? ModConfig.GatherStone.Value : null;
                case 3: return ModConfig.GatherOre != null ? ModConfig.GatherOre.Value : null;
            }
            return null;
        }
        static int DefaultLimit(int mode)
        {
            BepInEx.Configuration.ConfigEntry<int> e = mode == 1 ? ModConfig.WoodLimit : mode == 2 ? ModConfig.StoneLimit : mode == 3 ? ModConfig.OreLimit : null;
            return e != null ? e.Value : 500;
        }
        // 1.4.106: the shared list format (MHR_List): *, Name, -Name, Name:Number. Null when the list isn't set at all
        // (then everything is allowed).
        static MHR_List Parse(int mode)
        {
            string raw = ListFor(mode);
            if (raw == null) return null;
            MHR_List l = MHR_List.Get(raw);
            foreach (string n in l.Order) CheckKnown(n, mode);
            return l;
        }
        static string ItemLocal(string prefab)
        {
            try
            {
                GameObject g = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
                ItemDrop d = g != null ? g.GetComponent<ItemDrop>() : null;
                return d != null ? MHR_List.Local(d.m_itemData.m_shared.m_name) : null;
            }
            catch { return null; }
        }
        static void CheckKnown(string name, int mode)
        {
            if (!s_checkedNames.Add(mode + ":" + name)) return;
            try
            {
                if (ObjectDB.instance != null && ObjectDB.instance.m_items != null && ObjectDB.instance.m_items.Count > 0 && ObjectDB.instance.GetItemPrefab(name) == null)
                    Warn("\"" + name + "\" in " + (mode == 1 ? "GatherWood" : mode == 2 ? "GatherStone" : "GatherOre") + " isn't an item the game knows - check the spelling");
            }
            catch { }
        }
        static bool OnAnyList(string name)
        {
            for (int m = 1; m <= 3; m++)
            {
                MHR_List d = Parse(m);
                if (d != null && d.Allows(name)) return true;
            }
            return false;
        }

        // ---------- what a target drops ----------
        static void AddTable(DropTable t, List<string> outList)
        {
            if (t == null || t.m_drops == null) return;
            for (int i = 0; i < t.m_drops.Count; i++)
            {
                GameObject g = t.m_drops[i].m_item;
                if (g != null && !outList.Contains(g.name)) outList.Add(g.name);
            }
        }
        static void AddLog(GameObject logPrefab, List<string> outList, int depth)
        {
            if (logPrefab == null || depth > 4) return;
            TreeLog tl = logPrefab.GetComponent<TreeLog>();
            if (tl == null) return;
            AddTable(tl.m_dropWhenDestroyed, outList);
            AddLog(tl.m_subLogPrefab, outList, depth + 1);
        }
        static List<string> DropsOf(GameObject go)
        {
            List<string> l;
            int id = go.GetInstanceID();
            if (s_dropsCache.TryGetValue(id, out l)) return l;
            l = new List<string>();
            TreeBase tb = go.GetComponent<TreeBase>();
            if (tb != null) { AddTable(tb.m_dropWhenDestroyed, l); AddLog(tb.m_logPrefab, l, 0); }
            TreeLog tl = go.GetComponent<TreeLog>();
            if (tl != null) { AddTable(tl.m_dropWhenDestroyed, l); AddLog(tl.m_subLogPrefab, l, 0); }
            MineRock mr = go.GetComponent<MineRock>();
            if (mr != null) AddTable(mr.m_dropItems, l);
            MineRock5 mr5 = go.GetComponent<MineRock5>();
            if (mr5 != null) AddTable(mr5.m_dropItems, l);
            DropOnDestroyed dod = go.GetComponent<DropOnDestroyed>();
            if (dod != null) AddTable(dod.m_dropWhenDestroyed, l);
            if (s_dropsCache.Count > 2000) s_dropsCache.Clear();
            s_dropsCache[id] = l;
            return l;
        }

        // ---------- stock counting ----------
        static readonly List<Piece> s_piles = new List<Piece>();
        internal static int Stock(Component c, string item)
        {
            float t;
            if (s_countTime.TryGetValue(item, out t) && Time.time - t < 10f) return s_countVal[item];
            int total = 0;
            Humanoid h = c.GetComponent<Humanoid>();
            if (h != null && h.GetInventory() != null) total += CountIn(h.GetInventory(), item);
            CompanionSetup cs = c.GetComponent<CompanionSetup>();
            Vector3 center = (cs != null && cs.HasHomePosition()) ? cs.GetHomePosition() : c.transform.position;
            List<Container> near = MHR_Chests.Near(center, 40f);
            for (int i = 0; i < near.Count; i++)
            {
                Container box = near[i];
                if (box == null) continue;
                if (box.GetComponentInParent<Character>() != null || box.GetComponent<TombStone>() != null) continue;
                if (box.GetInventory() != null) total += CountIn(box.GetInventory(), item);
            }
            // 1.4.127: what's stacked in wood/stone piles counts too (user) - each pile holds what it was built from
            s_piles.Clear();
            Piece.GetAllPiecesInRadius(center, 40f, s_piles);
            for (int i = 0; i < s_piles.Count; i++)
            {
                Piece p = s_piles[i];
                if (!UpkeepController.IsPile(p)) continue;
                Piece.Requirement req = p.m_resources[0];
                if (string.Equals(req.m_resItem.gameObject.name, item, StringComparison.OrdinalIgnoreCase)) total += req.m_amount;
            }
            s_countTime[item] = Time.time; s_countVal[item] = total;
            return total;
        }
        static int CountIn(Inventory inv, string prefab)
        {
            int n = 0;
            List<ItemDrop.ItemData> items = inv.GetAllItems();
            for (int i = 0; i < items.Count; i++)
                if (items[i] != null && items[i].m_dropPrefab != null && string.Equals(items[i].m_dropPrefab.name, prefab, StringComparison.OrdinalIgnoreCase)) n += items[i].m_stack;
            return n;
        }

        // ---------- scan hooks ----------
        public static GameObject Scan(HarvestController h, int mode)
        {
            // 1.4.73: ore job - once a full stack of stone is carried, pile it out of the way before the next rock
            if (mode == 3)
            {
                Humanoid sh = h.GetComponent<Humanoid>();
                if (sh != null && sh.GetInventory() != null && CountIn(sh.GetInventory(), "Stone") >= 50 && StartPile(h)) return null;
            }
            Per p = P(h);
            p.LimitRejects = 0; p.OtherRejects = 0;
            GameObject r = h.ScanForTarget(mode);
            // Fase 3.2: when an order's ceiling is what stopped this scan, the caller ends the order instead
            // (MHR_GatherRules.OrderDone) - the "at its limit - waiting" line is for the config's own limits.
            bool byCeiling = p.CapRejects > 0 && p.CapFrame == Time.frameCount;
            if (r == null && !byCeiling && p.LimitRejects > 0 && mode >= 1 && mode <= 3 && Time.time >= p.NextPlenty)
            {
                p.NextPlenty = Time.time + 120f;
                Info("Everything it can reach on the " + (mode == 1 ? "wood" : mode == 2 ? "stone" : "ore") + " list is at its limit - waiting");
                CompanionTalk talk = h.GetComponent<CompanionTalk>();
                if (talk != null) talk.Say(ModLocalization.Loc(mode == 1 ? "hc_speech_plenty_wood" : mode == 2 ? "hc_speech_plenty_stone" : "hc_speech_plenty_ore"), false);
            }
            return r;
        }

        // Fase 3.2: a candidate was turned away because the ceiling an order put on this job is met. Counted per frame
        // (the frame's first look at a target or a loose item starts the count), so what "stopped it" is judged on
        // what this scan cycle actually saw - and never more than one ZDO read per candidate.
        static void CapReject(Per p)
        {
            if (p.CapFrame != Time.frameCount) { p.CapFrame = Time.frameCount; p.CapRejects = 0; }
            p.CapRejects++;
        }

        // Fase 3.2: a scan came up empty and the ceiling an order put on this job is what stopped it ("colete 5
        // madeiras") - the order is done: forget the ceiling and go back to following, like "stop". Called by
        // HarvestController when a scan finds nothing, which also covers the jobs that only pick things up by hand.
        public static bool OrderDone(HarvestController h, int mode)
        {
            try
            {
                if (h == null || mode < 1 || mode > 3) return false;
                if (h.IsDirectedHarvest) return false;      // a target pointed at on the radial is not a chat order
                Per p;
                // the rejection has to be from this frame's scan - a stale count must never end an order
                if (!s_per.TryGetValue(h.GetInstanceID(), out p) || p.CapRejects <= 0 || p.CapFrame != Time.frameCount) return false;
                int cap = CompanionSetup.OrderCap(h, mode);
                if (cap <= 0) return false;
                p.CapRejects = 0;
                Info("Order ceiling reached (" + cap + " on " + (mode == 1 ? "wood" : mode == 2 ? "stone" : "ore") + ") - order done, back to following");
                CompanionTalk talk = h.GetComponent<CompanionTalk>();
                if (talk != null) talk.Say(ModLocalization.Loc(mode == 1 ? "hc_speech_plenty_wood" : mode == 2 ? "hc_speech_plenty_stone" : "hc_speech_plenty_ore"), false);
                CompanionSetup.CompleteOrderCap(h);
                return true;
            }
            catch (Exception e) { Warn("OrderDone: " + e.Message); return false; }
        }

        public static bool AllowTarget(HarvestController h, GameObject go, int mode)
        {
            try
            {
                if (go == null || mode < 1 || mode > 3) return true;
                Per p = P(h);
                bool standingTree = go.GetComponent<TreeBase>() != null;
                if (standingTree)
                {
                    string skip = ModConfig.WoodSkipTrees != null ? ModConfig.WoodSkipTrees.Value : "";
                    if (!string.IsNullOrEmpty(skip))
                        foreach (string part in skip.Split(','))
                        {
                            string s = part.Trim();
                            if (s.Length > 0 && go.name.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0) { p.OtherRejects++; return false; }
                        }
                    if (NearBuildings(go)) { p.OtherRejects++; return false; }
                }
                MHR_List list = Parse(mode);
                if (list == null) return true;
                List<string> drops = DropsOf(go);
                if (drops.Count == 0) return true;      // unknown drops: don't block it
                bool listed = false;
                // Fase 3.2: the ceiling an order put on this job ("colete 5 madeiras") caps the job's own limit, so
                // 5 wins over WoodLimit 500; a smaller per-item amount (Name:Number) still wins over the ceiling.
                // A target the player pointed at on the radial is left alone.
                int cap = h.IsDirectedHarvest ? 0 : CompanionSetup.OrderCap(h, mode);
                for (int i = 0; i < drops.Count; i++)
                {
                    if (!list.Allows(drops[i], ItemLocal(drops[i]))) continue;
                    int limit = list.AmountFor(drops[i], DefaultLimit(mode));
                    bool capped = false;
                    if (cap > 0 && (limit <= 0 || cap < limit)) { limit = cap; capped = true; }
                    listed = true;
                    if (limit <= 0 || Stock(h, drops[i]) < limit) return true;
                    if (capped) CapReject(p);
                }
                if (listed) p.LimitRejects++; else p.OtherRejects++;
                return false;
            }
            catch (Exception e) { Warn("AllowTarget: " + e.Message); return true; }
        }

        static bool NearBuildings(GameObject tree)
        {
            float r = ModConfig.BaseProtectRadius != null ? ModConfig.BaseProtectRadius.Value : 10f;
            if (r <= 0f) return false;
            int id = tree.GetInstanceID();
            KeyValuePair<float, bool> cached;
            if (s_protect.TryGetValue(id, out cached) && Time.time - cached.Key < 30f) return cached.Value;
            bool near = false;
            s_pieces.Clear();
            Piece.GetAllPiecesInRadius(tree.transform.position, r, s_pieces);
            for (int i = 0; i < s_pieces.Count && !near; i++)
            {
                Piece pc = s_pieces[i];
                if (pc != null && pc.IsPlacedByPlayer()) near = true;
            }
            if (s_protect.Count > 2000) s_protect.Clear();
            s_protect[id] = new KeyValuePair<float, bool>(Time.time, near);
            return near;
        }

        // ---------- drops ----------
        // 1.4.122: loose things lying on the ground that the Wood job (branches -> Wood) and the Stone job (stones, flint)
        // pick up by hand - on the job's list and under its amount, like any other target
        public static bool LooseItem(int mode, string item)
        {
            return (mode == 1 && item == "Wood") || (mode == 2 && (item == "Stone" || item == "Flint"));
        }

        public static bool AllowLoose(HarvestController h, Pickable pick, int mode)
        {
            try
            {
                if (pick == null || pick.m_itemPrefab == null) return false;
                string item = pick.m_itemPrefab.name;
                if (!LooseItem(mode, item)) return false;
                MHR_List list = Parse(mode);
                if (list == null) return true;
                if (!list.Allows(item, ItemLocal(item))) return false;
                int limit = list.AmountFor(item, DefaultLimit(mode));
                // Fase 3.2: an order's ceiling caps this too, so "colete 5 madeiras" also stops picking branches up at 5
                int cap = h.IsDirectedHarvest ? 0 : CompanionSetup.OrderCap(h, mode);
                bool capped = false;
                if (cap > 0 && (limit <= 0 || cap < limit)) { limit = cap; capped = true; }
                if (limit <= 0 || Stock(h, item) < limit) return true;
                if (capped) CapReject(P(h));
                return false;
            }
            catch (Exception e) { Warn("AllowLoose: " + e.Message); return false; }
        }

        public static bool WantDrop(HarvestController h, ItemDrop drop)
        {
            try
            {
                if (drop == null || drop.m_itemData == null) return true;
                if (!drop.m_autoPickup) return false;           // junk he dropped, stone piles
                int mode = Mode(h);
                if (mode < 1 || mode > 3) return true;
                MHR_List list = Parse(mode);
                if (list == null) return true;
                string name = drop.m_itemData.m_dropPrefab != null ? drop.m_itemData.m_dropPrefab.name : drop.gameObject.name.Replace("(Clone)", "");
                if (mode == 3 && name == "Stone") return true;   // 1.4.73: cleared up into a pile out of the way
                return list.Allows(name, MHR_List.Local(drop.m_itemData.m_shared.m_name));
            }
            catch { return true; }
        }

        // Called when a wanted drop won't fit. Returns true if room was made right now.
        public static bool TryMakeRoom(HarvestController h, ItemDrop.ItemData incoming)
        {
            try
            {
                Humanoid hum = h.GetComponent<Humanoid>();
                if (hum == null || incoming == null) return false;
                Inventory inv = hum.GetInventory();
                if (inv == null) return false;
                CompanionSetup cs = h.GetComponent<CompanionSetup>();
                Per p = P(h);
                if (Mode(h) == 3 && StartPile(h)) return false;
                if (h.FindNearestChest(cs != null && cs.HasHomePosition() ? cs.GetHomePosition() : h.transform.position) != null)
                {
                    if (!p.PackFull) Info("Pack is full - taking a load to the chest");
                    p.PackFull = true;           // IsOverweight() now reports true until a slot frees up
                    return false;
                }
                if (ModConfig.JunkCleanup == null || !ModConfig.JunkCleanup.Value) return false;
                List<ItemDrop.ItemData> items = new List<ItemDrop.ItemData>(inv.GetAllItems());
                int dropped = 0; string names = "";
                for (int i = 0; i < items.Count && !inv.CanAddItem(incoming, incoming.m_stack); i++)
                {
                    ItemDrop.ItemData it = items[i];
                    if (!IsJunk(hum, it)) continue;
                    string nm = it.m_dropPrefab != null ? it.m_dropPrefab.name : it.m_shared.m_name;
                    Vector3 at = hum.transform.position + hum.transform.forward * 0.8f + Vector3.up * 0.5f;
                    ItemDrop d = ItemDrop.DropItem(it, it.m_stack, at, Quaternion.identity);
                    if (d == null) continue;
                    d.m_autoPickup = false;          // so neither it nor anyone's auto-pickup grabs it straight back
                    inv.RemoveItem(it);
                    dropped++; names += (names.Length > 0 ? ", " : "") + nm;
                }
                if (dropped > 0) Info("Dropped " + dropped + " junk item(s) to make room: " + names);
                return inv.CanAddItem(incoming, incoming.m_stack);
            }
            catch (Exception e) { Warn("TryMakeRoom: " + e.Message); return false; }
        }
        static bool IsJunk(Humanoid hum, ItemDrop.ItemData it)
        {
            if (it == null || it.m_shared == null || it.m_equipped || hum.IsItemEquiped(it)) return false;
            switch (it.m_shared.m_itemType)
            {
                case ItemDrop.ItemData.ItemType.Material:
                case ItemDrop.ItemData.ItemType.Trophy:
                case ItemDrop.ItemData.ItemType.Misc:
                    break;
                default: return false;      // gear, tools, ammo, food, potions
            }
            if (it.m_shared.m_food > 0f || it.m_shared.m_foodStamina > 0f) return false;
            string name = it.m_dropPrefab != null ? it.m_dropPrefab.name : "";
            if (name.Length == 0) return false;
            if (OnAnyList(name)) return false;
            if (FarmController.s_seedToPlant != null && FarmController.s_seedToPlant.ContainsKey(name)) return false;
            string keep = ModConfig.NeverDiscard != null ? ModConfig.NeverDiscard.Value : "";
            if (!string.IsNullOrEmpty(keep))
                foreach (string part in keep.Split(','))
                {
                    string s = part.Trim();
                    if (s.Length > 0 && (name.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        (it.m_shared.m_name != null && it.m_shared.m_name.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0))) return false;
                }
            return true;
        }

        public static bool PackFull(HarvestController h)
        {
            Per p;
            if (!s_per.TryGetValue(h.GetInstanceID(), out p) || !p.PackFull) return false;
            Humanoid hum = h.GetComponent<Humanoid>();
            if (hum == null || hum.GetInventory() == null || hum.GetInventory().HaveEmptySlot()) { p.PackFull = false; return false; }
            return true;
        }

        // ---------- falling trees ----------
        public static void OnTargetDestroyed(HarvestController h, string targetType)
        {
            try
            {
                if (targetType != "TreeBase" && targetType != "TreeLog") return;
                Transform t = h.transform;
                Vector3 fwd = t.forward; fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.01f) return;
                fwd.Normalize();
                Vector3 side = new Vector3(-fwd.z, 0f, fwd.x);
                Vector3 a = t.position + side * 4f, b = t.position - side * 4f;
                float ha = Ground(a), hb = Ground(b);
                Vector3 pick = ha >= hb ? a : b;          // uphill side: rolling logs go downhill
                Per p = P(h);
                p.DodgePoint = pick; p.DodgeUntil = Time.time + 2.5f;
                Debug("Stepping aside from the falling " + (targetType == "TreeBase" ? "tree" : "log"));
            }
            catch { }
        }
        public static bool TickDodge(HarvestController h)
        {
            Per p;
            if (!s_per.TryGetValue(h.GetInstanceID(), out p)) return false;
            if (p.PileActive)
            {
                CompanionAI pai = h.GetComponent<CompanionAI>();
                if (pai == null || pai.IsInCombat) return false;
                float d = new Vector2(h.transform.position.x - p.PilePoint.x, h.transform.position.z - p.PilePoint.z).magnitude;
                if (d <= 1.5f || Time.time >= p.PileUntil)
                {
                    pai.StopMoving();
                    DropStone(h, p);
                    p.PileActive = false;
                    h.ResetToIdle();
                    return true;
                }
                pai.MHR_Go(Time.deltaTime, p.PilePoint, 1.0f, false, "Pile");
                return true;
            }
            if (Time.time >= p.DodgeUntil) return false;
            CompanionAI ai = h.GetComponent<CompanionAI>();
            if (ai == null || ai.IsInCombat) { p.DodgeUntil = 0f; return false; }
            if (Vector3.Distance(h.transform.position, p.DodgePoint) > 0.6f) ai.MHR_Go(Time.deltaTime, p.DodgePoint, 0.4f, true, "Dodge");
            else ai.StopMoving();
            return true;
        }

        // ---------- 1.4.73: overweight, drop-off containers, stone piles, logs ----------
        // Replaces the old overweight block in HarvestController.Update (which flickered in and out of the job every frame
        // when there was no chest, and switched the job off when there was no home).
        public static void HandleOverweight(HarvestController h)
        {
            try
            {
                Per p = P(h); float now = Time.time;
                if (now - p.LastHeavyCall > 1.5f) { p.TooHeavy = false; p.NextDropOffCheck = 0f; }
                p.LastHeavyCall = now;
                if (p.PileActive) return;
                if (Mode(h) == 3 && StartPile(h)) return;
                CompanionAI ai = h.GetComponent<CompanionAI>();
                Humanoid hum = h.GetComponent<Humanoid>();
                if (now >= p.NextDropOffCheck)
                {
                    p.NextDropOffCheck = now + 5f;
                    CompanionSetup cs = h.GetComponent<CompanionSetup>();
                    Container c = h.FindNearestChest(cs != null && cs.HasHomePosition() ? cs.GetHomePosition() : h.transform.position);
                    if (c != null && ai != null && hum != null)
                    {
                        Info("Too heavy - taking a load to \"" + c.m_name + "\" " + Vector3.Distance(h.transform.position, c.transform.position).ToString("F0") + "m away");
                        h.ResetToIdle();
                        h.RestoreLoadout();
                        ai.SetPendingDeposit(c, hum);
                        p.TooHeavy = false; p.PackFull = false;
                        return;
                    }
                }
                if (!p.TooHeavy)
                {
                    p.TooHeavy = true;
                    Info("Too heavy and nowhere to put things - pausing the job until there's room");
                    h.ResetToIdle();
                    h.RestoreLoadout();
                }
                if (now >= p.NextHeavySpeak)
                {
                    p.NextHeavySpeak = now + 60f;
                    CompanionTalk t = h.GetComponent<CompanionTalk>();
                    if (t != null) t.Say(ModLocalization.Loc("hc_speech_overweight"), false);
                }
            }
            catch (Exception e) { Warn("HandleOverweight: " + e.Message); }
        }

        // Replaces HarvestController.FindNearestChest. Never tombstones. Near home: chests around home. Away from home:
        // chests, carts and ships close to the companion (an outpost, the cart you brought, your ship).
        public static Container DropOff(HarvestController h, Vector3 searchCenter)
        {
            try
            {
                CompanionSetup cs = h.GetComponent<CompanionSetup>();
                Vector3 me = h.transform.position;
                bool atHome = cs != null && cs.HasHomePosition() && MHR_ToolKeeper.NearHome(cs);
                List<Container> list = atHome ? MHR_Chests.Near(cs.GetHomePosition(), 50f) : MHR_Chests.Near(me, 30f);
                Container best = null; float bd = float.MaxValue;
                for (int i = 0; i < list.Count; i++)
                {
                    Container c = list[i];
                    if (c == null || c.GetInventory() == null || !c.GetInventory().HaveEmptySlot()) continue;
                    if (c.GetComponent<TombStone>() != null || c.GetComponentInParent<TombStone>() != null) continue;
                    if (c.GetComponentInParent<Character>() != null) continue;
                    bool mobile = c.GetComponentInParent<Vagon>() != null || c.GetComponentInParent<Ship>() != null;
                    if (mobile && atHome) continue;                   // at home, chests only
                    if (MHR_Chests.InUse(c)) continue;   // 1.4.153: also open on another player's game
                    if (!PrivateArea.CheckAccess(c.transform.position, 0f, flash: false)) continue;
                    ZNetView nv = c.GetComponent<ZNetView>();
                    if (nv == null || !nv.IsValid()) continue;
                    if (c.m_privacy == Container.PrivacySetting.Private)
                    {
                        Piece pc = c.GetComponent<Piece>();
                        if (pc == null || pc.GetCreator() != MHR_Owner.CreatorId(h)) continue;
                    }
                    float d = Vector3.Distance(me, c.transform.position);
                    if (d >= bd) continue;
                    if (!mobile && !MHR_Chests.CanReach(me, c)) continue;
                    bd = d; best = c;
                }
                return best;
            }
            catch (Exception e) { Warn("DropOff: " + e.Message); return null; }
        }

        // Replaces HarvestController.TryFindNearbyLog: the nearest log near the fallen tree, and only if the gather
        // list / limits allow it.
        public static GameObject NearestLog(HarvestController h, Vector3 center)
        {
            try
            {
                int mode = Mode(h);
                GameObject best = null; float bd = float.MaxValue;
                Collider[] cols = Physics.OverlapSphere(center, 12f);
                HashSet<int> seen = new HashSet<int>();
                for (int i = 0; i < cols.Length; i++)
                {
                    if (cols[i] == null) continue;
                    TreeLog log = cols[i].GetComponentInParent<TreeLog>();
                    if (log == null || !seen.Add(log.GetInstanceID())) continue;
                    GameObject go = log.gameObject;
                    if (h._blacklist != null && h._blacklist.ContainsKey(go.GetInstanceID())) continue;
                    if (!AllowTarget(h, go, mode)) continue;
                    float d = Vector3.Distance(center, go.transform.position);
                    if (d < bd) { bd = d; best = go; }
                }
                return best;
            }
            catch { return null; }
        }

        // Where to look for drops after something breaks: rocks drop at the rock, trees around the companion.
        public static Vector3 DropCenter(HarvestController h)
        {
            string t = h._targetType;
            if ((t == "MineRock" || t == "MineRock5" || t == "Destructible") && h._attackNodePos != Vector3.zero) return h._attackNodePos;
            return h.transform.position;
        }

        public static void BlacklistTarget(HarvestController h)
        {
            try { if (h._target != null && h._blacklist != null) h._blacklist[h._target.GetInstanceID()] = Time.time; } catch { }
        }

        // Ore mode: put the stone in a pile a few meters away instead of hauling it home.
        public static bool StartPile(HarvestController h)
        {
            Per p = P(h);
            if (p.PileActive) return true;
            Humanoid hum = h.GetComponent<Humanoid>();
            if (hum == null || hum.GetInventory() == null || CountIn(hum.GetInventory(), "Stone") <= 0) return false;
            Vector3 me = h.transform.position;
            if (!p.PileHas || Vector3.Distance(me, p.PileSpot) > 25f)
            {
                Vector3 ore = h._attackNodePos != Vector3.zero ? h._attackNodePos : me;
                Vector3 dir = me - ore; dir.y = 0f;
                if (dir.sqrMagnitude < 0.25f) dir = h.transform.right;
                dir.Normalize();
                dir = Quaternion.Euler(0f, 50f, 0f) * dir;       // off to the side, not on the way back to the rock
                Vector3 spot = ore + dir * 6f;
                spot.y = Ground(spot);
                p.PileSpot = spot; p.PileHas = true;
            }
            p.PilePoint = p.PileSpot; p.PileActive = true; p.PileUntil = Time.time + 25f;
            Info("Taking the stone to a pile out of the way");
            return true;
        }
        static void DropStone(HarvestController h, Per p)
        {
            Humanoid hum = h.GetComponent<Humanoid>();
            if (hum == null || hum.GetInventory() == null) return;
            Inventory inv = hum.GetInventory();
            List<ItemDrop.ItemData> items = new List<ItemDrop.ItemData>(inv.GetAllItems());
            int n = 0;
            for (int i = 0; i < items.Count; i++)
            {
                ItemDrop.ItemData it = items[i];
                if (it == null || it.m_dropPrefab == null || it.m_dropPrefab.name != "Stone" || it.m_equipped) continue;
                Vector3 at = p.PilePoint + Vector3.up * 0.4f + new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), 0f, UnityEngine.Random.Range(-0.3f, 0.3f));
                int count = it.m_stack;
                ItemDrop d = ItemDrop.DropItem(it, count, at, Quaternion.identity);
                if (d == null) continue;
                d.m_autoPickup = false;      // stays in the pile until someone picks it up on purpose
                inv.RemoveItem(it);
                n += count;
            }
            P(h).PackFull = false;
            if (n > 0) Info("Piled " + n + " stone");
        }

        // Approach trees and logs from the uphill side when the ground slopes.
        public static Vector3 ApproachPoint(HarvestController h, Vector3 point)
        {
            try
            {
                GameObject go = h._target;
                if (go == null || (go.GetComponent<TreeBase>() == null && go.GetComponent<TreeLog>() == null)) return point;
                Per p = P(h);
                int id = go.GetInstanceID();
                if (p.ApproachFor != id)
                {
                    p.ApproachFor = id; p.ApproachHas = false;
                    float h0 = Ground(point), best = h0; Vector3 bestDir = Vector3.zero;
                    for (int i = 0; i < 8; i++)
                    {
                        float ang = i * Mathf.PI / 4f;
                        Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                        float g = Ground(point + dir * 2.5f);
                        if (g > best) { best = g; bestDir = dir; }
                    }
                    if (best - h0 > 0.4f) { p.ApproachHas = true; p.ApproachOffset = bestDir * 0.8f; }
                }
                return p.ApproachHas ? point + p.ApproachOffset : point;
            }
            catch { return point; }
        }

        static float Ground(Vector3 pos)
        {
            return ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(pos) : pos.y;
        }
        static int Mode(Component c)
        {
            ZNetView nv = c.GetComponent<ZNetView>();
            return (nv != null && nv.IsValid() && nv.GetZDO() != null) ? nv.GetZDO().GetInt(CompanionSetup.ActionModeHash, 0) : 0;
        }
        static void Info(string m) { if (CompanionsPlugin.Log != null) CompanionsPlugin.Log.LogInfo("[Gather] " + m); }
        static void Debug(string m) { if (CompanionsPlugin.Log != null) CompanionsPlugin.Log.LogDebug("[Gather] " + m); }
        static void Warn(string m) { if (CompanionsPlugin.Log != null) CompanionsPlugin.Log.LogWarning("[Gather] " + m); }
    }
}
