using System;
using System.Collections.Generic;
using UnityEngine;

namespace Companions
{
    // ---- 1.4.62/1.4.65: seed stock rules for the farmer ----
    public static class MHR_SeedRules
    {
        // Fase 3.19 (auditoria): per companion - the total includes the companion's own pack (see MHR_StockKey)
        static readonly Dictionary<MHR_StockKey, float> s_countTime = new Dictionary<MHR_StockKey, float>();
        static readonly Dictionary<MHR_StockKey, int> s_countVal = new Dictionary<MHR_StockKey, int>();

        /// <summary>Fase 3.19: forgets every cached total. The cache is keyed by (companion, item), so a change in one
        /// pack can't be invalidated by name any more - and this only runs after the farm moved something.</summary>
        static void InvalidateCount()
        {
            s_countTime.Clear();
            s_countVal.Clear();
        }

        // What planting this item grows into (the item the grown plant drops), or null.
        static string OutputOf(string item)
        {
            FarmController.SeedPlantInfo info;
            if (FarmController.s_seedToPlant == null || !FarmController.s_seedToPlant.TryGetValue(item, out info) || info.PlantPrefab == null) return null;
            Plant plant = info.PlantPrefab.GetComponent<Plant>();
            if (plant == null || plant.m_grownPrefabs == null) return null;
            for (int i = 0; i < plant.m_grownPrefabs.Length; i++)
            {
                GameObject g = plant.m_grownPrefabs[i];
                if (g == null) continue;
                Pickable p = g.GetComponent<Pickable>();
                if (p != null && p.m_itemPrefab != null) return p.m_itemPrefab.name;
            }
            return null;
        }

        // A "seed source" is a crop that, when planted, grows a plant that drops seeds (Carrot -> carrot seed plant -> CarrotSeeds).
        public static bool IsSeedSource(string item)
        {
            FarmController.SeedPlantInfo info;
            if (string.IsNullOrEmpty(item) || FarmController.s_seedToPlant == null || !FarmController.s_seedToPlant.TryGetValue(item, out info) || info.PlantPrefab == null) return false;
            if (info.PlantPrefab.name.IndexOf("seed", StringComparison.OrdinalIgnoreCase) < 0) return false;
            string output = OutputOf(item);
            return output != null && output != item && FarmController.s_seedToPlant.ContainsKey(output);
        }

        // Farm storage: a crop is stored unless it's a seed the farmer plants (seeds and self-replanting crops stay in the pack).
        public static bool IsStorableCrop(string item)
        {
            if (string.IsNullOrEmpty(item) || FarmController.s_seedToPlant == null) return true;
            if (!FarmController.s_seedToPlant.ContainsKey(item)) return true;
            return IsSeedSource(item);
        }

        // 1.4.67: FarmController.FindChestWithSpecificSeed looked chests up with Inventory.GetItem(prefabName), which matches
        // the item's display-name key ($item_...) instead, so it never found anything. Same call shape, prefab match.
        public static ItemDrop.ItemData GetItemByPrefab(Inventory inv, string prefab, int quality, bool worstQuality)
        {
            if (inv == null || string.IsNullOrEmpty(prefab)) return null;
            List<ItemDrop.ItemData> items = inv.GetAllItems();
            for (int i = 0; i < items.Count; i++)
                if (items[i] != null && items[i].m_dropPrefab != null && items[i].m_dropPrefab.name == prefab) return items[i];
            return inv.GetItem(prefab, quality, worstQuality);
        }

        static int Min() { return ModConfig.SeedReserve != null ? Mathf.Max(0, ModConfig.SeedReserve.Value) : 20; }
        static int Target() { return Mathf.Max(Min(), ModConfig.SeedTarget != null ? ModConfig.SeedTarget.Value : 100); }
        // 1.4.70: vegetables it's carrying to grow seed plants stay in the pack while that seed is under SeedTarget
        // (1.4.69 took turnips for seed plants, then stored them straight back, over and over)
        static bool NeededForSeeds(Component farm, string item)
        {
            if (!IsSeedSource(item)) return false;
            string seeds = OutputOf(item);
            return seeds != null && CountNear(farm, seeds) < Target();
        }

        public static int Carry() { return Mathf.Max(1, ModConfig.SeedCarry != null ? ModConfig.SeedCarry.Value : 40); }
        static int Keep() { return Mathf.Max(Min(), Carry()); }
        static int GroupSize() { return Mathf.Max(1, ModConfig.PlantGroupSize != null ? ModConfig.PlantGroupSize.Value : 4); }

        // Old Any-zone hooks (FindPlantableSeedInInventory / FindChestWithSeeds). Any zones now go through PickSeed,
        // but keep these consistent: seed plants only while that seed is under the target.
        public static bool AllowPlant(Component farm, string item)
        {
            try
            {
                if (!IsSeedSource(item)) return true;
                return CountNear(farm, OutputOf(item)) < Target();
            }
            catch { return true; }
        }

        // ---- 1.4.65: crop families, the PlantAny list, seed minimum/target, planting in groups ----
        class Fam { public string Seed; public string Source; }
        class Rot { public string Cur; public int Count; }
        static readonly Dictionary<int, Dictionary<string, Rot>> s_rot = new Dictionary<int, Dictionary<string, Rot>>();
        static readonly Dictionary<string, string> s_sourceFor = new Dictionary<string, string>();
        static readonly Dictionary<string, string> s_lower = new Dictionary<string, string>();
        static int s_mapCount = -1;
        static string s_listRaw;
        static List<Fam> s_list = new List<Fam>();
        static readonly HashSet<string> s_warned = new HashSet<string>();
        static float s_nextNothingLog;

        static void CheckMap()
        {
            int n = FarmController.s_seedToPlant != null ? FarmController.s_seedToPlant.Count : 0;
            if (n == s_mapCount) return;
            s_mapCount = n; s_sourceFor.Clear(); s_lower.Clear(); s_listRaw = null;
            if (FarmController.s_seedToPlant == null) return;
            List<string> names = new List<string>(FarmController.s_seedToPlant.Keys);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug("[Farm] Things a cultivator can plant (" + names.Count + "): " + string.Join(", ", names.ToArray()));
            foreach (string k in FarmController.s_seedToPlant.Keys)
            {
                s_lower[k.ToLowerInvariant()] = k;
                if (!IsSeedSource(k)) continue;
                string o = OutputOf(k);
                if (o != null && !s_sourceFor.ContainsKey(o)) s_sourceFor[o] = k;
            }
        }

        // What family a plantable item belongs to: CarrotSeeds/Carrot -> {CarrotSeeds, Carrot}; Barley -> {Barley, null}
        static Fam Resolve(string name)
        {
            if (string.IsNullOrEmpty(name) || FarmController.s_seedToPlant == null) return null;
            if (!FarmController.s_seedToPlant.ContainsKey(name))
            {
                string real;
                if (!s_lower.TryGetValue(name.ToLowerInvariant(), out real))
                {
                    // 1.4.67: short names work too (JotunPuffs -> MushroomJotunPuffs) when only one item ends with it
                    string low = name.ToLowerInvariant(); real = null; int hits = 0;
                    foreach (KeyValuePair<string, string> kv in s_lower)
                        if (kv.Key.EndsWith(low)) { real = kv.Value; hits++; }
                    if (hits != 1) return null;
                }
                name = real;
            }
            if (IsSeedSource(name)) return new Fam { Seed = OutputOf(name), Source = name };
            string src;
            return new Fam { Seed = name, Source = s_sourceFor.TryGetValue(name, out src) ? src : null };
        }

        // 1.4.106: the shared list format (MHR_List): * = every crop that grows on cultivated ground (not tree
        // saplings), Name, -Name, Name:Number (stop planting it once there are that many of its crop)
        static List<Fam> ListFams()
        {
            string raw = ModConfig.PlantAny != null ? ModConfig.PlantAny.Value : "";
            if (raw == s_listRaw) return s_list;
            s_listRaw = raw;
            s_list = new List<Fam>();
            MHR_List l = MHR_List.Get(raw);
            HashSet<string> seen = new HashSet<string>();
            List<string> names = new List<string>(l.Order);
            if (l.All && FarmController.s_seedToPlant != null)
                foreach (KeyValuePair<string, FarmController.SeedPlantInfo> kv in FarmController.s_seedToPlant)
                    if (kv.Value.NeedsCultivated && !names.Contains(kv.Key)) names.Add(kv.Key);
            foreach (string name in names)
            {
                Fam f = Resolve(name);
                if (f == null)
                {
                    if (s_warned.Add(name) && CompanionsPlugin.Log != null)
                        CompanionsPlugin.Log.LogWarning("[Farm] PlantAny: \"" + name + "\" isn't something a cultivator can plant - skipped");
                    continue;
                }
                if (l.IsExcluded(name, f.Seed, f.Source)) continue;
                if (seen.Add(f.Seed)) s_list.Add(f);
            }
            return s_list;
        }

        // Name:Number on PlantAny: the crop's own stock target (counting what planting it gives: the vegetable for seeds,
        // the item itself for barley, flax...)
        static int CropAmount(Fam f)
        {
            MHR_List l = MHR_List.Get(ModConfig.PlantAny != null ? ModConfig.PlantAny.Value : "");
            int n = l.AmountFor(f.Seed, 0);
            if (n <= 0 && f.Source != null) n = l.AmountFor(f.Source, 0);
            return n;
        }

        static Rot GetRot(Component farm, string key)
        {
            Dictionary<string, Rot> d;
            int id = farm.GetInstanceID();
            if (!s_rot.TryGetValue(id, out d)) { d = new Dictionary<string, Rot>(); s_rot[id] = d; }
            Rot r;
            if (!d.TryGetValue(key, out r)) { r = new Rot(); d[key] = r; }
            return r;
        }

        // Which item to plant next in a zone. zoneCrop empty = an "Any" zone (uses the PlantAny list).
        // Returns null when nothing should be planted right now.
        public static string PickSeed(Component farm, string zoneCrop)
        {
            try
            {
                if (farm == null || FarmController.s_seedToPlant == null) return string.IsNullOrEmpty(zoneCrop) ? null : zoneCrop;
                CheckMap();
                List<Fam> fams;
                string key = string.IsNullOrEmpty(zoneCrop) ? "*" : zoneCrop;
                if (!string.IsNullOrEmpty(zoneCrop))
                {
                    Fam f = Resolve(zoneCrop);
                    if (f == null || zoneCrop == f.Source) return zoneCrop;   // unknown, or a zone set to grow seed plants on purpose
                    fams = new List<Fam> { f };
                }
                else fams = ListFams();

                int min = Min(), target = Target(), size = GroupSize();
                List<string> order = new List<string>();
                List<bool> ok = new List<bool>();
                System.Text.StringBuilder report = new System.Text.StringBuilder();
                for (int i = 0; i < fams.Count; i++)
                {
                    Fam f = fams[i];
                    int seeds = CountNear(farm, f.Seed);
                    int veg = f.Source != null ? CountNear(farm, f.Source) : 0;
                    if (report.Length > 0) report.Append("; ");
                    report.Append(f.Seed).Append(' ').Append(seeds);
                    if (f.Source != null) report.Append('/').Append(min).Append(", ").Append(f.Source).Append(' ').Append(veg);
                    order.Add(f.Seed);
                    // seeds: above the minimum, or (1.4.66) any at all when there's nothing to grow seed plants from yet
                    bool canPlant = f.Source == null ? seeds > 0 : (seeds > min || (seeds > 0 && veg == 0));
                    int crop = CropAmount(f);
                    if (crop > 0 && (f.Source != null ? veg : seeds) >= crop) { canPlant = false; report.Append(" (enough, ").Append(crop).Append(')'); }
                    ok.Add(canPlant);
                    if (f.Source != null)
                    {
                        order.Add(f.Source);
                        ok.Add(seeds < target && veg > 0);
                    }
                }
                int n = order.Count;
                if (n == 0) return null;
                Rot r = GetRot(farm, key);
                int cur = r.Cur != null ? order.IndexOf(r.Cur) : -1;
                if (cur >= 0 && ok[cur] && r.Count < size) return r.Cur;
                for (int step = 1; step <= n; step++)
                {
                    int idx = ((cur + step) % n + n) % n;
                    if (!ok[idx]) continue;
                    r.Cur = order[idx]; r.Count = 0;
                    if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug("[Farm] Planting group: next up \"" + r.Cur + "\" (" + (key == "*" ? "Any zone" : "zone " + key) + ")");
                    return r.Cur;
                }
                if (Time.time >= s_nextNothingLog && CompanionsPlugin.Log != null)
                {
                    s_nextNothingLog = Time.time + 60f;
                    if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Farm] Nothing to plant in " + (key == "*" ? "Any zones" : "\"" + key + "\" zones") + " (pack + chests near home): " + (report.Length > 0 ? report.ToString() : "the PlantAny list is empty"));
                }
                return null;
            }
            catch (Exception e)
            {
                if (CompanionsPlugin.Log != null) CompanionsPlugin.Log.LogWarning("[Farm] PickSeed: " + e.Message);
                return string.IsNullOrEmpty(zoneCrop) ? null : zoneCrop;
            }
        }

        // 1.4.68: after each plant the farmer keeps going with the same seed; this tells it when its group is done so it
        // goes back to scanning and PickSeed can switch to the next type.
        public static bool GroupDone(Component farm, string item)
        {
            try
            {
                Dictionary<string, Rot> d;
                if (farm == null || item == null || !s_rot.TryGetValue(farm.GetInstanceID(), out d)) return false;
                int size = GroupSize();
                foreach (Rot r in d.Values) if (r.Cur == item && r.Count >= size) return true;
            }
            catch { }
            return false;
        }

        // 1.4.69: while it has a seed chest open, top up every type it may plant (up to SeedCarry each) so it isn't walking
        // back for each type. Vegetables for seed plants only while that seed is under SeedTarget.
        public static int TakeExtras(Component farm, Inventory chest, Inventory pack)
        {
            int took = 0;
            try
            {
                if (farm == null || chest == null || pack == null || FarmController.s_seedToPlant == null) return 0;
                CheckMap();
                int carry = Carry(), target = Target();
                List<Fam> fams = ListFams();
                List<string> want = new List<string>();
                for (int i = 0; i < fams.Count; i++)
                {
                    want.Add(fams[i].Seed);
                    if (fams[i].Source != null && CountNear(farm, fams[i].Seed) < target) want.Add(fams[i].Source);
                }
                for (int w = 0; w < want.Count; w++)
                {
                    string name = want[w];
                    int need = carry - CountIn(pack, name);
                    if (need <= 0) continue;
                    List<ItemDrop.ItemData> items = new List<ItemDrop.ItemData>(chest.GetAllItems());
                    for (int i = 0; i < items.Count && need > 0; i++)
                    {
                        ItemDrop.ItemData it = items[i];
                        if (it == null || it.m_dropPrefab == null || it.m_dropPrefab.name != name) continue;
                        int n = Mathf.Min(need, it.m_stack);
                        ItemDrop.ItemData part = it.Clone(); part.m_stack = n;
                        if (!pack.CanAddItem(part, n) || !pack.AddItem(part)) break;
                        if (n >= it.m_stack) chest.RemoveItem(it); else chest.RemoveItem(it, n);
                        need -= n; took += n;
                        InvalidateCount();
                        if (CompanionsPlugin.Log != null) CompanionsPlugin.Log.LogDebug("[Farm]   Also took " + n + "x \"" + name + "\"");
                    }
                }
            }
            catch (Exception e) { if (CompanionsPlugin.Log != null) CompanionsPlugin.Log.LogWarning("[Farm] TakeExtras: " + e.Message); }
            return took;
        }

        public static void OnPlanted(Component farm, string item)
        {
            try
            {
                if (farm == null || item == null) return;
                InvalidateCount();
                Dictionary<string, Rot> d;
                if (!s_rot.TryGetValue(farm.GetInstanceID(), out d)) return;
                foreach (Rot r in d.Values) if (r.Cur == item) r.Count++;
            }
            catch { }
        }

        // Farm storage. Crops go to the chest. Plantable items (seeds, barley, flax...) keep the minimum in the pack
        // so there is always something to plant; only the extra is stored.
        public static bool WantsStore(Component farm, ItemDrop.ItemData item)
        {
            if (item == null || item.m_dropPrefab == null) return false;
            string name = item.m_dropPrefab.name;
            if (IsStorableCrop(name) && !NeededForSeeds(farm, name)) return true;
            Humanoid h = farm != null ? farm.GetComponent<Humanoid>() : null;
            if (h == null || h.GetInventory() == null) return false;
            return CountIn(h.GetInventory(), name) > Keep();
        }

        public static int StoreOutput(FarmController farm, Inventory pack, Inventory chest)
        {
            int stored = 0;
            if (farm == null || pack == null || chest == null) return 0;
            List<ItemDrop.ItemData> items = new List<ItemDrop.ItemData>(pack.GetAllItems());
            for (int i = items.Count - 1; i >= 0; i--)
            {
                ItemDrop.ItemData it = items[i];
                try
                {
                    if (it == null || it.m_dropPrefab == null || !farm.IsCropItem(it)) continue;
                    string name = it.m_dropPrefab.name;
                    int n = it.m_stack;
                    if (!IsStorableCrop(name) || NeededForSeeds(farm, name))
                    {
                        int extra = CountIn(pack, name) - Keep();
                        if (extra <= 0) continue;
                        n = Mathf.Min(extra, it.m_stack);
                    }
                    ItemDrop.ItemData part = it.Clone();
                    part.m_stack = n;
                    if (!chest.CanAddItem(part, n) || !chest.AddItem(part)) continue;
                    if (n >= it.m_stack) pack.RemoveItem(it); else pack.RemoveItem(it, n);
                    InvalidateCount();
                    stored++;
                    if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug("[Farm]   Stored \"" + name + "\" x" + n);
                }
                catch (Exception e) { if (CompanionsPlugin.Log != null) CompanionsPlugin.Log.LogWarning("[Farm] Store: " + e.Message); }
            }
            return stored;
        }

        static int CountNear(Component farm, string prefab)
        {
            // Fase 3.19 (auditoria): the total carries the companion's own pack, so the cache is per companion
            MHR_StockKey key = new MHR_StockKey(farm.GetInstanceID(), prefab);
            float t;
            if (s_countTime.TryGetValue(key, out t) && Time.time - t < 10f) return s_countVal[key];
            int total = 0;
            Humanoid h = farm.GetComponent<Humanoid>();
            if (h != null && h.GetInventory() != null) total += CountIn(h.GetInventory(), prefab);
            CompanionSetup cs = farm.GetComponent<CompanionSetup>();
            Vector3 center = (cs != null && cs.HasHomePosition()) ? cs.GetHomePosition() : farm.transform.position;
            List<Container> near = MHR_Chests.Near(center, 30f);
            for (int i = 0; i < near.Count; i++)
            {
                Container c = near[i];
                if (c == null || c.GetComponentInParent<Character>() != null || c.GetComponent<TombStone>() != null) continue;
                if (c.GetInventory() != null) total += CountIn(c.GetInventory(), prefab);
            }
            s_countTime[key] = Time.time; s_countVal[key] = total;
            return total;
        }
        static int CountIn(Inventory inv, string prefab)
        {
            int n = 0;
            List<ItemDrop.ItemData> items = inv.GetAllItems();
            for (int i = 0; i < items.Count; i++)
                if (items[i] != null && items[i].m_dropPrefab != null && items[i].m_dropPrefab.name == prefab) n += items[i].m_stack;
            return n;
        }
    }
}
