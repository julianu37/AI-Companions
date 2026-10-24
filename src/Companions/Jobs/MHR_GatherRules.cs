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
            // Fase 3.2: candidates this frame's scan turned away because an order's target was met (see CapReject)
            public int CapRejects; public int CapFrame;
            // Fase 3.3d: is the number an order asked for already in the pack? Decided once per frame per mode (see
            // OrderMet), so the scan's walk over the candidates costs one inventory count, not one per candidate.
            public bool OrderFull; public int OrderFullFrame = -1; public int OrderFullMode = -1;
            public float NextPlenty;
            public bool PackFull;
            public float DodgeUntil; public Vector3 DodgePoint;
            public int ApproachFor; public Vector3 ApproachOffset; public bool ApproachHas;
            public bool TooHeavy; public float LastHeavyCall, NextDropOffCheck, NextHeavySpeak;
            public bool PileActive; public float PileUntil; public Vector3 PilePoint; public bool PileHas; public Vector3 PileSpot;
        }
        static readonly Dictionary<int, Per> s_per = new Dictionary<int, Per>();
        static readonly List<Piece> s_pieces = new List<Piece>();
        static readonly Dictionary<MHR_StockKey, float> s_countTime = new Dictionary<MHR_StockKey, float>();
        static readonly Dictionary<MHR_StockKey, int> s_countVal = new Dictionary<MHR_StockKey, int>();
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
        // (then everything is allowed). Internal so the chat orders can resolve the item an order named against the
        // job's own list (Fase 3.2c, see OrderItems).
        internal static MHR_List Parse(int mode)
        {
            string raw = ListFor(mode);
            if (raw == null) return null;
            MHR_List l = MHR_List.Get(raw);
            foreach (string n in l.Order) CheckKnown(n, mode);
            return l;
        }
        // The in-game (localized) name of an item prefab, for matching names typed as they appear in game.
        internal static string ItemLocal(string prefab)
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
            AddDrops(go, l, 0);
            if (s_dropsCache.Count > 2000) s_dropsCache.Clear();
            s_dropsCache[id] = l;
            return l;
        }
        // Fase 3.3d: what a thing leaves behind when it breaks is often where the loot really is. Rock_3 has no drop
        // table at all (health 1) - its stone comes from the Rock_3_frac it spawns when destroyed (a MineRock5, 3-6
        // Stone per hit area). Judged only by its own components the rock looked like it drops nothing, and a target
        // with no known drops is never blocked ("unknown drops: don't block it"), so a stone order was never capped
        // there. Depth-limited (a spawn chain, never a loop) and cached per target by DropsOf, so it costs nothing
        // per frame - once per target, and only for things that actually spawn something.
        static void AddDrops(GameObject go, List<string> outList, int depth)
        {
            if (go == null || depth > 3) return;
            TreeBase tb = go.GetComponent<TreeBase>();
            if (tb != null) { AddTable(tb.m_dropWhenDestroyed, outList); AddLog(tb.m_logPrefab, outList, 0); }
            TreeLog tl = go.GetComponent<TreeLog>();
            if (tl != null) { AddTable(tl.m_dropWhenDestroyed, outList); AddLog(tl.m_subLogPrefab, outList, 0); }
            MineRock mr = go.GetComponent<MineRock>();
            if (mr != null) AddTable(mr.m_dropItems, outList);
            MineRock5 mr5 = go.GetComponent<MineRock5>();
            if (mr5 != null) AddTable(mr5.m_dropItems, outList);
            DropOnDestroyed dod = go.GetComponent<DropOnDestroyed>();
            if (dod != null) AddTable(dod.m_dropWhenDestroyed, outList);
            Destructible des = go.GetComponent<Destructible>();
            if (des != null)
            {
                AddDrops(des.m_spawnWhenDestroyed, outList, depth + 1);
                AddDrops(des.m_spawnWhenDamaged, outList, depth + 1);
            }
        }

        // ---------- stock counting ----------
        static readonly List<Piece> s_piles = new List<Piece>();
        internal static int Stock(Component c, string item)
        {
            // Fase 3.19 (auditoria): the total carries the companion's own pack, so the cache is per companion
            MHR_StockKey key = new MHR_StockKey(c.GetInstanceID(), item);
            float t;
            if (s_countTime.TryGetValue(key, out t) && Time.time - t < 10f) return s_countVal[key];
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
            s_countTime[key] = Time.time; s_countVal[key] = total;
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
            // Fase 3.2: when an order's target is what stopped this scan, the caller ends the order instead
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

        // Fase 3.2: a candidate was turned away because the target an order put on this job is met. Counted per frame
        // (the frame's first look at a target or a loose item starts the count), so what "stopped it" is judged on
        // what this scan cycle actually saw - and never more than one ZDO read per candidate. Fase 3.2c: the same
        // count decides that the order is done when the ordered item is at its number in the pack.
        static void CapReject(Per p)
        {
            if (p.CapFrame != Time.frameCount) { p.CapFrame = Time.frameCount; p.CapRejects = 0; }
            p.CapRejects++;
        }

        // Fase 3.2c: the item the job's own list named, if the order named one the game knows. Returns "" when
        // there is no order for this job, when the order named no item at all, or when the target was pointed at
        // on the radial (that is not a chat order).
        static string OrderedItem(HarvestController h, int mode)
        {
            return (h != null && !h.IsDirectedHarvest) ? CompanionSetup.OrderCapItem(h, mode) : "";
        }

        // Fase 3.3d: the item an order named is already in the companion's own pack at the number asked, so from
        // here on EVERY target of that mode is turned away - not only the ones the classifier knows produce that
        // item. Without it a target whose drops it cannot see (a rock that only carries loot in what it spawns when
        // it breaks, see AddDrops) kept the job alive after the number was reached: the order never ended and the
        // companion mined stone well past "colete 10 pedras". Judged on the same count as the rest of Fase 3.2c -
        // the pack, remembering the highest it has held (OrderSeen), so unloading into a chest doesn't start the
        // order over - and never for a target the player pointed at on the radial (that is not a chat order).
        // The answer is remembered for the frame (and the mode), so a scan pays for it once, not per candidate.
        static bool OrderMet(HarvestController h, int mode)
        {
            if (h == null) return false;
            Per p = P(h);
            if (p.OrderFullFrame != Time.frameCount || p.OrderFullMode != mode)
            {
                p.OrderFullFrame = Time.frameCount;
                p.OrderFullMode = mode;
                string ordered = OrderedItem(h, mode);
                // "" = no order for this job, another mode's order, or a target the player pointed at on the radial
                p.OrderFull = ordered.Length > 0 && CompanionSetup.OrderSeen(h, Bag(h, ordered)) >= CompanionSetup.OrderCap(h, mode);
            }
            return p.OrderFull;
        }

        // Fase 3.2c: what the companion is carrying in its OWN pack. An order's number is a total counted here -
        // not in the chests near home or in the wood/stone piles, which is what Stock() counts for the config's own
        // limits (WoodLimit & co. mean "we have plenty"; the player's order means "have this much on you").
        // The pack is a small list and GetAllItems() hands back the inventory's own list, so this allocates nothing.
        internal static int Bag(Component c, string item)
        {
            Humanoid h = c != null ? c.GetComponent<Humanoid>() : null;
            Inventory inv = h != null ? h.GetInventory() : null;
            return inv != null ? CountIn(inv, item) : 0;
        }

        // Fase 3.2/3.2c: the limit that applies to ONE item of the job's list, and whether the order's number is
        // what set it. The item an order named is not decided here - AllowTarget/AllowLoose judge it on its own,
        // in the pack (see the orderedHere branch). This covers the rest of the list, and, when the order named no
        // item the game knows, every item (then the number caps the whole job, as it did in Fase 3.2).
        static int LimitFor(HarvestController h, int mode, string item, MHR_List list, out bool capped)
        {
            capped = false;
            int limit = list.AmountFor(item, DefaultLimit(mode));
            if (OrderedItem(h, mode).Length > 0) return limit;   // the order is about one item: this one is the config's
            int cap = CompanionSetup.OrderCap(h, mode);
            if (cap <= 0) return limit;                          // no order: the config rules, as before
            if (limit <= 0 || !list.HasAmount(item) || cap < limit)
            {
                capped = true;
                return cap;      // what the player asked for wins over the config's "we have plenty"
            }
            return limit;
        }

        // Fase 3.2c: how much of one item the job goes by - the pack when an order's number decided the limit, the
        // whole stock (pack + chests + piles) when the config did, exactly as before this phase.
        static int Have(HarvestController h, string item, bool capped)
        {
            return capped ? Bag(h, item) : Stock(h, item);
        }

        // Fase 3.2: a scan came up empty and the target an order put on this job is what stopped it ("colete 5
        // madeiras") - the order is done: forget it and go back to following, like "stop". Called by
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
                if (CompanionSetup.OrderCap(h, mode) <= 0) return false;
                p.CapRejects = 0;
                return FinishOrder(h, mode);
            }
            catch (Exception e) { Warn("OrderDone: " + e.Message); return false; }
        }

        // Fase 3.3d: the same ending, but decided on the pack alone - for the case the number asked for is reached
        // while the companion is still working a target. The scan between targets (OrderDone) catches that too, only
        // it may take a whole rock to get there: a stone rock's loot comes from what it spawns when it breaks (see
        // AddDrops), so "colete 10 pedras" could go on for 20 or 30 before the next scan ever ran. Called by
        // HarvestController while it is moving to or working a target - never while it is picking up what that target
        // dropped, so nothing it has just mined is left lying on the ground.
        public static bool OrderDoneNow(HarvestController h, int mode)
        {
            try
            {
                if (h == null || mode < 1 || mode > 3) return false;
                if (h.IsDirectedHarvest) return false;
                if (!OrderMet(h, mode)) return false;
                Per p;
                if (s_per.TryGetValue(h.GetInstanceID(), out p)) p.CapRejects = 0;
                return FinishOrder(h, mode);
            }
            catch (Exception e) { Warn("OrderDoneNow: " + e.Message); return false; }
        }

        // The order is met: say so once, forget the number and go back to following, exactly like "stop".
        static bool FinishOrder(HarvestController h, int mode)
        {
            int cap = CompanionSetup.OrderCap(h, mode);
            if (cap <= 0) return false;
            string item = CompanionSetup.OrderCapItem(h, mode);
            Info("Order target reached (" + cap + " " + (item.Length > 0 ? item + " in the pack" : (mode == 1 ? "wood" : mode == 2 ? "stone" : "ore")) + ") - order done, back to following");
            CompanionTalk talk = h.GetComponent<CompanionTalk>();
            if (talk != null) talk.Say(ModLocalization.Loc(mode == 1 ? "hc_speech_plenty_wood" : mode == 2 ? "hc_speech_plenty_stone" : "hc_speech_plenty_ore"), false);
            CompanionSetup.CompleteOrderCap(h);
            return true;
        }

        public static bool AllowTarget(HarvestController h, GameObject go, int mode)
        {
            try
            {
                if (go == null || mode < 1 || mode > 3) return true;
                Per p = P(h);
                // Fase 3.3d: the order's number is already in the pack - nothing of this mode is worked any more, so
                // the scan comes up empty and OrderDone ends the order (once) and sends the companion back to
                // following, exactly like "stop". Counted as a ceiling rejection, not as a limit one: the config's
                // "at its limit - waiting" line is not about a player order.
                if (OrderMet(h, mode)) { CapReject(p); return false; }
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
                string ordered = OrderedItem(h, mode);
                bool listed = false, orderedHere = false;
                for (int i = 0; i < drops.Count; i++)
                {
                    if (!list.Allows(drops[i], ItemLocal(drops[i]))) continue;
                    listed = true;
                    if (ordered.Length > 0 && string.Equals(drops[i], ordered, StringComparison.OrdinalIgnoreCase)) orderedHere = true;
                }
                if (!listed) { p.OtherRejects++; return false; }
                // Fase 3.2c: when the order named this item and the target drops it, the order decides alone - a tree
                // is not chopped just because its resin is still under WoodLimit (the Wood list carries Resin too,
                // and before this phase the target was kept for it). The number itself is judged at the top of this
                // method (OrderMet): this is only reached while the pack is still short of what was asked.
                if (orderedHere) return true;
                for (int i = 0; i < drops.Count; i++)
                {
                    if (!list.Allows(drops[i], ItemLocal(drops[i]))) continue;
                    // Fase 3.2/3.2c: the number an order asked for caps this item when the order named no item the
                    // game knows; a smaller per-item amount (Name:Number) still wins, and a target the player
                    // pointed at on the radial is left alone.
                    bool capped;
                    int limit = LimitFor(h, mode, drops[i], list, out capped);
                    if (limit <= 0 || Have(h, drops[i], capped) < limit) return true;
                    if (capped) CapReject(p);
                }
                p.LimitRejects++;
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
                // Fase 3.3d: the order's number is already in the pack - what is lying on the ground is left there
                // too, so the loose scan ends the order the same way a target does (see OrderMet).
                if (OrderMet(h, mode)) { CapReject(P(h)); return false; }
                MHR_List list = Parse(mode);
                if (list == null) return true;
                if (!list.Allows(item, ItemLocal(item))) return false;
                // Fase 3.2c: an order that named this item counts it in the pack, so "colete 10 madeiras" stops
                // picking branches up at 10 in the pack even with a chest full of wood at home. The number itself is
                // judged at the top of this method (OrderMet): this is only reached while the pack is short of it.
                if (string.Equals(item, OrderedItem(h, mode), StringComparison.OrdinalIgnoreCase)) return true;
                // Fase 3.2: an order that named no item the game knows caps this too, so "colete 5" also stops
                // picking things up at 5 (Fase 3.2c: counted in the pack, see Have).
                bool capped;
                int limit = LimitFor(h, mode, item, list, out capped);
                if (limit <= 0 || Have(h, item, capped) < limit) return true;
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
