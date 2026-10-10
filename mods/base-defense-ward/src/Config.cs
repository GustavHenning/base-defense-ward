using BepInEx.Configuration;

namespace BaseDefenseWard
{
    // All tunables. File: BepInEx/config/com.night.basedefenseward.cfg
    public static class Cfg
    {
        public static ConfigEntry<float> CountdownMinutes;
        public static ConfigEntry<float> WaveTimeLimitMinutes;
        public static ConfigEntry<float> SpawnRadius;
        public static ConfigEntry<int> BaseMobCount;
        public static ConfigEntry<int> MobsPerCompletedChallenge;
        public static ConfigEntry<int> MaxMobCount;
        public static ConfigEntry<float> SpawnIntervalSeconds;
        public static ConfigEntry<float> RestMinMinutes;
        public static ConfigEntry<float> RestMaxMinutes;
        public static ConfigEntry<bool> PausableWards;
        public static ConfigEntry<KeyboardShortcut> PauseKey;
        public static ConfigEntry<bool> StartNow;
        public static ConfigEntry<float> PlayerHuntRange;
        public static ConfigEntry<float> RewardMultiplier;
        public static ConfigEntry<bool> DeleteWorldOnLoss;
        public static ConfigEntry<float> BuildDeadlineMinutes;
        public static ConfigEntry<string> BossKeys;
        public static ConfigEntry<string> ExtraMobPools;

        public static void Bind(ConfigFile c)
        {
            CountdownMinutes = c.Bind("Challenge", "CountdownMinutes", 10f,
                "Minutes between building the ward and the first wave.");
            WaveTimeLimitMinutes = c.Bind("Challenge", "WaveTimeLimitMinutes", 5f,
                "If the ward still stands after this many minutes the challenge is won even if mobs remain.");
            SpawnRadius = c.Bind("Challenge", "SpawnRadius", 55f,
                "Distance from the ward at which wave mobs spawn (the ward's protected radius is 32).");
            BaseMobCount = c.Bind("Challenge", "BaseMobCount", 4, "Mobs in the first wave.");
            MobsPerCompletedChallenge = c.Bind("Challenge", "MobsPerCompletedChallenge", 2,
                "Extra mobs per previously completed challenge on this world.");
            MaxMobCount = c.Bind("Challenge", "MaxMobCount", 30, "Hard cap on mobs per wave.");
            SpawnIntervalSeconds = c.Bind("Challenge", "SpawnIntervalSeconds", 1.5f, "Delay between individual spawns.");
            RestMinMinutes = c.Bind("Challenge", "RestMinMinutes", 30f,
                "After a successful defense the ward rests for a random time between RestMinMinutes and RestMaxMinutes before the next countdown starts.");
            RestMaxMinutes = c.Bind("Challenge", "RestMaxMinutes", 120f, "Upper bound of the rest between challenges, in minutes.");
            PausableWards = c.Bind("Challenge", "PausableWards", false,
                "Allow pausing a ward (its countdown and rest timers stop) by pressing PauseKey while standing within interact range of it. Not possible during a wave.");
            PauseKey = c.Bind("Challenge", "PauseKey", new KeyboardShortcut(UnityEngine.KeyCode.P), "Key that pauses or resumes the ward you stand next to (only when PausableWards is on).");
            StartNow = c.Bind("Challenge", "StartNow", true,
                "Interacting with the ward (Use key) during the countdown starts the wave immediately instead of toggling the ward.");
            PlayerHuntRange = c.Bind("Challenge", "PlayerHuntRange", 40f,
                "Mobs switch to hunting a player who is within this range of them and not sheltered (roofed and walled in). Otherwise they go for the ward.");
            RewardMultiplier = c.Bind("Rewards", "RewardMultiplier", 1f, "Scales all reward amounts.");
            DeleteWorldOnLoss = c.Bind("Consequences", "DeleteWorldOnLoss", false,
                "If the ward is destroyed during a wave (or the build deadline is missed) the world save is deleted and the game exits. Dedicated servers exit so a wrapper script can restart them.");
            BuildDeadlineMinutes = c.Bind("Consequences", "BuildDeadlineMinutes", 0f,
                "If > 0, a ward must be built within this many minutes of world creation or the challenge counts as lost. 0 = off.");
            BossKeys = c.Bind("Progression", "BossKeys",
                "bdw_hung_eikthyr,bdw_hung_theelder,bdw_hung_bonemass,bdw_hung_dragonqueen,bdw_hung_yagluth,bdw_hung_thequeen,bdw_hung_fader",
                "Global keys in tier order; the highest tier whose key is set decides which mob pools are unlocked. The bdw_hung_* keys are set by this mod when a trophy is hung on the corresponding boss stone. To use boss kills instead: defeated_eikthyr,defeated_gdking,defeated_bonemass,defeated_dragon,defeated_goblinking,defeated_queen,defeated_fader");
            ExtraMobPools = c.Bind("Progression", "ExtraMobPools", "",
                "Optional override of the mob pools, one tier per ';', prefabs per ',' e.g. 'Greyling,Neck;Greydwarf,Skeleton'. Empty = built-in pools.");
        }
    }
}
