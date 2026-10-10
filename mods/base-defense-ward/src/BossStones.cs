using System.Collections.Generic;
using HarmonyLib;

namespace BaseDefenseWard
{
    // Vanilla boss stones have no world key of their own (m_setsWorldKey is empty), so "trophy hung up" leaves no
    // trace once you walk away. This mirrors the stone's state into a global key: bdw_hung_<boss>, e.g. bdw_hung_eikthyr.
    [HarmonyPatch(typeof(BossStone), "SetActivated")]
    static class BossStone_SetActivated
    {
        static readonly Dictionary<string, bool> Last = new Dictionary<string, bool>();

        public static string KeyFor(BossStone stone)
        {
            var n = stone.gameObject.name.Replace("(Clone)", "").Trim();
            if (n.StartsWith("BossStone_")) n = n.Substring("BossStone_".Length);
            return "bdw_hung_" + n.ToLowerInvariant();
        }

        static void Postfix(BossStone __instance, bool active)
        {
            if (ZoneSystem.instance == null || __instance.m_itemStand == null) return;
            var key = KeyFor(__instance);
            if (Last.TryGetValue(key, out var prev) && prev == active) return;
            Last[key] = active;
            if (active) { if (!ZoneSystem.instance.GetGlobalKey(key)) ZoneSystem.instance.SetGlobalKey(key); }
            else if (ZoneSystem.instance.GetGlobalKey(key)) ZoneSystem.instance.RemoveGlobalKey(key);
        }
    }
}
