using System.Globalization;

namespace BaseDefenseWard
{
    // "Ward must be built within X minutes of world creation." Checked once a second on the server.
    public static class Deadline
    {
        static float _timer;

        public static void Tick(float dt)
        {
            _timer -= dt;
            if (_timer > 0f) return;
            _timer = 1f;
            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZoneSystem.instance == null) return;

            double now = ZNet.instance.GetTimeSeconds();
            if (!ZoneSystem.instance.GetGlobalKey(Progress.WorldStartKey, out var startStr))
            {
                // First time this mod sees the world: remember "now" as the start of the clock.
                Progress.SetValue(Progress.WorldStartKey, ((long)now).ToString(CultureInfo.InvariantCulture));
                return;
            }
            if (Cfg.BuildDeadlineMinutes.Value <= 0f) return;
            if (Progress.Has(Progress.BuiltKey) || Progress.Has(Progress.DeadlineFailedKey)) return;
            if (!long.TryParse(startStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var start)) return;
            if (now - start < Cfg.BuildDeadlineMinutes.Value * 60.0) return;

            ZoneSystem.instance.SetGlobalKey(Progress.DeadlineFailedKey);
            Plugin.Log.LogWarning("Build deadline missed: no Base Defense Ward was built in time");
            WardChallenge.Say("No ward was built in time. Challenge lost.");
            if (Cfg.DeleteWorldOnLoss.Value) WorldReset.Schedule("build deadline missed");
        }

        public static string Status()
        {
            if (ZNet.instance == null) return "no znet";
            ZoneSystem.instance.GetGlobalKey(Progress.WorldStartKey, out var s);
            return $"deadlineMin={Cfg.BuildDeadlineMinutes.Value} worldStart={s} now={ZNet.instance.GetTimeSeconds():0} built={Progress.Has(Progress.BuiltKey)} failed={Progress.Has(Progress.DeadlineFailedKey)}";
        }
    }
}
