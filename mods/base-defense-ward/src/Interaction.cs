using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace BaseDefenseWard
{
    // Player-facing controls on a loaded ward: interact (E) during the countdown starts the wave now (StartNow
    // option), and the pause hotkey within interact range freezes the countdown/rest (PausableWards option).
    public static class Interaction
    {
        public static WardChallenge NearestInReach(Player p)
        {
            float reach = p.m_maxInteractDistance;
            return WardChallenge.All.Where(w => w != null && w.View != null && w.View.IsValid())
                                    .Select(w => (w, d: Vector3.Distance(w.transform.position, p.transform.position)))
                                    .Where(t => t.d <= reach).OrderBy(t => t.d).Select(t => t.w).FirstOrDefault();
        }

        public static string TogglePause(Player p)
        {
            if (!Cfg.PausableWards.Value) return "Pausing wards is disabled in the config.";
            var w = NearestInReach(p);
            if (w == null) return "No Base Defense Ward within reach.";
            if (w.State == ChallengeState.Active) return "A wave is attacking. The ward cannot be paused now.";
            w.RequestTogglePause();
            return null;
        }

        public static string StartNow(WardChallenge w)
        {
            if (!Cfg.StartNow.Value) return "Starting the wave early is disabled in the config.";
            if (w.State != ChallengeState.Countdown) return null;
            w.RequestStartNow();
            return "The wave is called early!";
        }

        // Hotkey polled from Plugin.Update on every client.
        public static void Tick()
        {
            if (!Cfg.PausableWards.Value || Player.m_localPlayer == null || Console.IsVisible() || Chat.instance?.HasFocus() == true) return;
            if (!Cfg.PauseKey.Value.IsDown()) return;
            var msg = TogglePause(Player.m_localPlayer);
            if (msg != null) Player.m_localPlayer.Message(MessageHud.MessageType.Center, msg);
        }
    }

    [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.Interact))]
    static class PrivateArea_Interact
    {
        // During the countdown E starts the wave instead of toggling the ward's protection.
        static bool Prefix(PrivateArea __instance, Humanoid human, bool hold, ref bool __result)
        {
            if (hold || !Cfg.StartNow.Value) return true;
            var w = __instance.GetComponent<WardChallenge>();
            if (w == null || w.View == null || !w.View.IsValid() || w.State != ChallengeState.Countdown) return true;
            var msg = Interaction.StartNow(w);
            if (msg != null && human is Player p) p.Message(MessageHud.MessageType.Center, msg);
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.GetHoverText))]
    static class PrivateArea_GetHoverText
    {
        static void Postfix(PrivateArea __instance, ref string __result)
        {
            var w = __instance.GetComponent<WardChallenge>();
            if (w == null || w.View == null || !w.View.IsValid()) return;
            var lines = new System.Collections.Generic.List<string>();
            switch (w.State)
            {
                case ChallengeState.Countdown:
                    lines.Add($"Wave in {Fmt(Cfg.CountdownMinutes.Value * 60f - w.Elapsed)}{(w.Paused ? " (paused)" : "")}");
                    if (Cfg.StartNow.Value) lines.Add("[<color=yellow><b>$KEY_Use</b></color>] Start the wave now");
                    break;
                case ChallengeState.Active: lines.Add($"Wave! hold {Fmt(Cfg.WaveTimeLimitMinutes.Value * 60f - w.WaveElapsed)}"); break;
                case ChallengeState.Resting: lines.Add($"Defended. Next wave in {Fmt(w.RestFor - w.RestElapsed)}{(w.Paused ? " (paused)" : "")}"); break;
            }
            if (Cfg.PausableWards.Value && w.State != ChallengeState.Active)
                lines.Add($"[<color=yellow><b>{Cfg.PauseKey.Value.MainKey}</b></color>] {(w.Paused ? "Resume" : "Pause")} the ward");
            if (lines.Count > 0) __result = Localization.instance.Localize(__result + "\n" + string.Join("\n", lines));
        }

        static string Fmt(float s) { if (s < 0) s = 0; var t = System.TimeSpan.FromSeconds(s); return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes:00}:{t.Seconds:00}"; }
    }
}
