using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BaseDefenseWard
{
    // Which creatures a wave draws from, and what the reward looks like, per progression tier.
    public static class MobPool
    {
        // Tier index = number of boss keys required (0 = fresh spawn).
        static readonly string[][] Builtin =
        {
            new[] { "Greyling", "Neck", "Boar" },                                            // 0 Meadows
            new[] { "Greydwarf", "Greydwarf_Shaman", "Skeleton", "Greydwarf_Elite" },         // 1 Eikthyr -> Black Forest
            new[] { "Draugr", "Draugr_Ranged", "Blob", "Skeleton_Poison", "Troll" },          // 2 Elder -> Swamp
            new[] { "Wolf", "Draugr_Elite", "Fenring", "Ulv", "Wraith" },                     // 3 Bonemass -> Mountain
            new[] { "Goblin", "GoblinArcher", "GoblinShaman", "GoblinBrute" },               // 4 Moder -> Plains
            new[] { "Seeker", "SeekerBrute", "Tick", "Gjall" },                               // 5 Yagluth -> Mistlands
            new[] { "Charred_Melee", "Charred_Archer", "Charred_Mage", "Charred_Twitcher" },   // 6 Queen -> Ashlands
            new[] { "Charred_Melee", "Charred_Archer", "Charred_Mage", "Morgen" },            // 7 Fader
        };

        // Reward per tier: (prefab, base amount). Scaled by completed count and RewardMultiplier.
        static readonly (string, int)[][] Rewards =
        {
            new[] { ("Coins", 25), ("Flint", 10), ("LeatherScraps", 8) },
            new[] { ("Coins", 50), ("Copper", 6), ("Tin", 4), ("SurtlingCore", 2) },
            new[] { ("Coins", 80), ("Iron", 6), ("Bronze", 4), ("Chain", 1) },
            new[] { ("Coins", 120), ("Silver", 6), ("WolfPelt", 3), ("Obsidian", 6) },
            new[] { ("Coins", 160), ("BlackMetal", 6), ("LinenThread", 6), ("Needle", 2) },
            new[] { ("Coins", 220), ("BlackMarble", 12), ("Eitr", 6), ("Sap", 6) },
            new[] { ("Coins", 300), ("FlametalNew", 6), ("Grausten", 12), ("Blackwood", 12) },
            new[] { ("Coins", 400), ("FlametalNew", 10), ("Grausten", 16), ("CharredBone", 8) },
        };

        public static string[][] Pools()
        {
            var s = Cfg.ExtraMobPools.Value;
            if (string.IsNullOrWhiteSpace(s)) return Builtin;
            return s.Split(';').Select(t => t.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray()).ToArray();
        }

        // Sample `count` prefab names. Only tiers <= bossTier are eligible; higher tiers get heavier with completed challenges.
        public static List<string> Sample(int count, int bossTier, int completed, System.Random rng)
        {
            var pools = Pools();
            int maxTier = Mathf.Clamp(bossTier, 0, pools.Length - 1);
            var weights = new float[maxTier + 1];
            for (int t = 0; t <= maxTier; t++)
            {
                // Prefer the newest unlocked tier; older tiers fade as challenges are completed but never vanish.
                float recency = 1f + (t == maxTier ? 2f : 0f) + t * 0.5f + completed * 0.25f * t;
                weights[t] = recency;
            }
            var result = new List<string>();
            for (int i = 0; i < count; i++)
            {
                int tier = Weighted(weights, rng);
                var pool = pools[tier].Where(Exists).ToArray();
                if (pool.Length == 0) pool = pools[0].Where(Exists).ToArray();
                if (pool.Length == 0) break;
                result.Add(pool[rng.Next(pool.Length)]);
            }
            return result;
        }

        public static List<(string prefab, int amount)> RewardFor(int bossTier, int completed)
        {
            int tier = Mathf.Clamp(bossTier, 0, Rewards.Length - 1);
            float scale = (1f + 0.25f * completed) * Cfg.RewardMultiplier.Value;
            return Rewards[tier].Where(r => Exists(r.Item1))
                .Select(r => (r.Item1, Mathf.Max(1, Mathf.RoundToInt(r.Item2 * scale)))).ToList();
        }

        static bool Exists(string prefab) => ZNetScene.instance != null && ZNetScene.instance.GetPrefab(prefab) != null;

        static int Weighted(float[] w, System.Random rng)
        {
            float total = w.Sum(), r = (float)rng.NextDouble() * total;
            for (int i = 0; i < w.Length; i++) { r -= w[i]; if (r <= 0f) return i; }
            return w.Length - 1;
        }
    }
}
