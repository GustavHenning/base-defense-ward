using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BaseDefenseWard
{
    // World-wide progression stored in ZoneSystem global keys (synced to all peers, saved with the world).
    public static class Progress
    {
        public const string CompletedKey = "bdw_completed";
        public const string BuiltKey = "bdw_built";
        public const string WorldStartKey = "bdw_worldstart";
        public const string DeadlineFailedKey = "bdw_deadline_failed";

        public static int Completed()
        {
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(CompletedKey, out var v) &&
                int.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var n)) return n;
            return 0;
        }

        public static void SetCompleted(int n) => SetValue(CompletedKey, n.ToString(CultureInfo.InvariantCulture));

        public static void SetValue(string key, string value)
        {
            ZoneSystem.instance.RemoveGlobalKey(key);
            ZoneSystem.instance.SetGlobalKey(key + " " + value);
        }

        public static bool Has(string key) => ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(key);

        public static string[] BossKeys() =>
            Cfg.BossKeys.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();

        // 0 = no boss key set, 1 = first key set, ... Highest set key wins (a hung Elder trophy unlocks tier 2 even if Eikthyr's is missing).
        public static int BossTier()
        {
            var keys = BossKeys();
            int tier = 0;
            for (int i = 0; i < keys.Length; i++)
                if (Has(keys[i])) tier = i + 1;
            return tier;
        }

        public static string Summary() =>
            $"completed={Completed()} bossTier={BossTier()} keys=[{string.Join(",", BossKeys().Where(Has))}]";
    }
}
