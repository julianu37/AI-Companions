using System;
using HarmonyLib;
using UnityEngine;

namespace Companions
{
    // ---- 1.4.106: companions fish with the game's own float and fish (user: "it should fish for real"). The float
    // only knows players: it finds its owner in the player list by the caster's user id - for a companion that's the
    // player, so the float looked for a rod on the player and destroyed itself (and could even replace the player's own
    // float). The float now remembers the companion that cast it. ----
    public static class MHR_FishingFloat
    {
        internal const string OwnerHash = "HC_FloatOwner";

        internal static readonly AccessTools.FieldRef<FishingFloat, Fish> Nibbler = AccessTools.FieldRefAccess<FishingFloat, Fish>("m_nibbler");
        internal static readonly AccessTools.FieldRef<FishingFloat, float> NibbleTime = AccessTools.FieldRefAccess<FishingFloat, float>("m_nibbleTime");
        internal static readonly AccessTools.FieldRef<FishingFloat, float> LineLength = AccessTools.FieldRefAccess<FishingFloat, float>("m_lineLength");
        internal static readonly AccessTools.FieldRef<FishingFloat, ZNetView> NView = AccessTools.FieldRefAccess<FishingFloat, ZNetView>("m_nview");

        // the float this companion cast, if it's out
        public static FishingFloat Of(Character c)
        {
            if (c == null) return null;
            ZDOID me = c.GetZDOID();
            foreach (FishingFloat f in FishingFloat.GetAllInstances())
            {
                if (f == null) continue;
                ZNetView nv = NView(f);
                if (nv == null || !nv.IsValid()) continue;
                if (nv.GetZDO().GetZDOID(OwnerHash) == me) return f;
            }
            return null;
        }

        internal static Character CompanionOwner(FishingFloat f)
        {
            ZNetView nv = NView(f);
            if (nv == null || !nv.IsValid()) return null;
            ZDOID id = nv.GetZDO().GetZDOID(OwnerHash);
            if (id.IsNone() || ZNetScene.instance == null) return null;
            GameObject go = ZNetScene.instance.FindInstance(id);
            return go != null ? go.GetComponent<Character>() : null;
        }

        // 1.4.108: for a companion, Setup is done here instead of by the game. 1.4.107 test: the game's Setup threw a
        // NullReferenceException every frame when the float hit the water, so no float ever came out and the projectile
        // kept spawning floats. Same steps as vanilla, nothing assumed: bait name from the ammo (or the bait it has
        // ready), the rod tip found on the companion, the line length set; if something's missing it says what.
        [HarmonyPatch(typeof(FishingFloat), "Setup")]
        static class Setup_Patch
        {
            static bool Prefix(FishingFloat __instance, Character owner, ItemDrop.ItemData ammo)
            {
                try
                {
                    if (owner == null || owner.GetComponent<CompanionSetup>() == null) return true;
                    ZNetView nv = NView(__instance);
                    if (nv == null || !nv.IsValid()) return false;
                    FishingFloat old = Of(owner);
                    if (old != null && old != __instance && ZNetScene.instance != null) ZNetScene.instance.Destroy(old.gameObject);
                    ZDO zdo = nv.GetZDO();
                    zdo.Set(OwnerHash, owner.GetZDOID());
                    zdo.Set(ZDOVars.s_rodOwner, owner.GetZDOID().UserID);
                    string bait = ammo != null && ammo.m_dropPrefab != null ? ammo.m_dropPrefab.name : null;
                    if (bait == null)
                    {
                        Humanoid h = owner as Humanoid;
                        ItemDrop.ItemData a = h != null ? h.GetAmmoItem() : null;
                        bait = a != null && a.m_dropPrefab != null ? a.m_dropPrefab.name : FishController.LastBait(owner);
                    }
                    if (bait != null) zdo.Set(ZDOVars.s_bait, bait);
                    Transform tip = FindChild(owner.transform, "_RodTop");
                    if (tip == null)
                    {
                        Warn("no rod tip (_RodTop) on " + owner.m_name + " - the float can't be used");
                        nv.Destroy();
                        return false;
                    }
                    if (__instance.m_rodLine != null) __instance.m_rodLine.SetPeer(owner.GetZDOID());
                    LineLength(__instance) = Vector3.Distance(tip.position, __instance.transform.position);
                    if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log?.LogDebug("[Fish] Float out " + LineLength(__instance).ToString("F0") + " m, bait " + (bait ?? "?") + (ammo == null ? " (the cast came without its ammo)" : ""));
                    return false;
                }
                catch (Exception e)
                {
                    Warn("float setup: " + e.GetType().Name + ": " + e.Message);
                    try { ZNetView nv = NView(__instance); if (nv != null && nv.IsValid()) nv.Destroy(); } catch { }
                    return false;
                }
            }
        }

        static float s_nextWarn;
        static void Warn(string msg)
        {
            if (Time.time < s_nextWarn) return;
            s_nextWarn = Time.time + 10f;
            CompanionsPlugin.Log?.LogWarning("[Fish] " + msg);
        }

        static Transform FindChild(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                Transform r = FindChild(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        [HarmonyPatch(typeof(FishingFloat), "GetOwner")]
        static class GetOwner_Patch
        {
            static bool Prefix(FishingFloat __instance, ref Character __result)
            {
                try
                {
                    ZNetView nv = NView(__instance);
                    if (nv == null || !nv.IsValid() || nv.GetZDO().GetZDOID(OwnerHash).IsNone()) return true;
                    __result = CompanionOwner(__instance);
                    return false;
                }
                catch { return true; }
            }
        }

        // a cast that caught nothing gives the bait back - vanilla only does that for players
        [HarmonyPatch(typeof(FishingFloat), "ReturnBait")]
        static class ReturnBait_Patch
        {
            static bool Prefix(FishingFloat __instance, bool ___m_baitConsumed)
            {
                try
                {
                    Character owner = CompanionOwner(__instance);
                    if (owner == null) return true;
                    if (!___m_baitConsumed)
                    {
                        Humanoid h = owner as Humanoid;
                        GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(__instance.GetBait()) : null;
                        if (h != null && prefab != null) h.GetInventory().AddItem(prefab, 1);
                    }
                    return false;
                }
                catch { return true; }
            }
        }
    }
}
