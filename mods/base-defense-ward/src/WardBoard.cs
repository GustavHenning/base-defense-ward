using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BaseDefenseWard
{
    // Everything a peer needs to show every ward in the world, at any distance:
    //  - the server advances countdown/rest timers of wards nobody is simulating (unloaded) while the builder
    //    is logged in, writing straight into the ZDO (the server holds every ZDO);
    //  - the server broadcasts a compact summary of all wards every 2 s ("BDW_Wards"); HUD and map pins read
    //    that, because a client only knows ZDOs near itself.
    public static class WardBoard
    {
        public class Entry
        {
            public ZDOID Id; public long Creator; public string Name; public ChallengeState State;
            public bool Paused, Running; public float Remaining; public Vector3 Pos; public float ReceivedAt;
            // Seconds left on the current timer, counted down locally between server updates while it runs.
            public float RemainingNow => Running ? Mathf.Max(0f, Remaining - (Time.time - ReceivedAt)) : Remaining;
        }

        public static readonly List<Entry> Entries = new List<Entry>();
        public static HashSet<long> Online = new HashSet<long>();   // profile ids of connected players (server-authoritative)
        public static float LastUpdate = -1f;
        public static bool IsOnline(long playerId) => Online.Contains(playerId);
        const string Rpc = "BDW_Wards";
        static bool _registered;
        static float _clockTimer, _sendTimer;
        static readonly int HState = "bdw_state".GetStableHashCode();

        public static void Tick(float dt)
        {
            if (ZRoutedRpc.instance == null || ZNet.instance == null) { _registered = false; return; }
            if (!_registered) { ZRoutedRpc.instance.Register<ZPackage>(Rpc, RPC_Wards); _registered = true; }
            if (!ZNet.instance.IsServer() || ZDOMan.instance == null) return;

            _clockTimer += dt; _sendTimer += dt;
            if (_clockTimer >= 1f) { AdvanceUnloaded(_clockTimer); _clockTimer = 0f; }
            if (_sendTimer >= 2f) { Broadcast(); _sendTimer = 0f; }
        }

        // ---- server: timers keep running for a logged-in builder even when the ward is far from everyone ----
        static void AdvanceUnloaded(float dt)
        {
            var online = OnlinePlayerIds();
            foreach (var z in AllWardZdos())
            {
                var state = (ChallengeState)z.GetInt(HState, 0);
                if (state != ChallengeState.Countdown && state != ChallengeState.Resting) continue;
                if (z.GetBool(WardChallenge.HPaused, false) || !online.Contains(z.GetLong(ZDOVars.s_creator))) continue;
                if (IsSimulated(z.GetPosition())) continue;   // a nearby peer's WardChallenge.Tick is counting
                int h = state == ChallengeState.Countdown ? WardChallenge.HElapsed : WardChallenge.HRestElapsed;
                z.Set(h, z.GetFloat(h, 0f) + dt);
            }
        }

        // Some peer (or the host itself) has this position inside its active area, so the object is loaded there.
        static bool IsSimulated(Vector3 pos)
        {
            if (!ZNet.instance.IsDedicated() && ZNetScene.InActiveArea(pos, ZNet.instance.GetReferencePosition())) return true;
            foreach (var peer in ZNet.instance.GetPeers()) if (peer.IsReady() && ZNetScene.InActiveArea(pos, peer.GetRefPos())) return true;
            return false;
        }

        // Profile ids of everyone connected (the server sees every player's ZDO, which carries the profile id).
        static HashSet<long> OnlinePlayerIds()
        {
            var ids = new HashSet<long>();
            if (Player.m_localPlayer != null) ids.Add(Player.m_localPlayer.GetPlayerID());
            foreach (var info in ZNet.instance.GetPlayerList())
            {
                var pz = ZDOMan.instance.GetZDO(info.m_characterID);
                long id = pz != null ? pz.GetLong(ZDOVars.s_playerID, 0L) : 0L;
                if (id != 0) ids.Add(id);
            }
            return ids;
        }

        public static string NameOf(long playerId, ZDO ward)
        {
            if (Player.m_localPlayer != null && Player.m_localPlayer.GetPlayerID() == playerId) return Player.m_localPlayer.GetPlayerName();
            foreach (var p in Player.GetAllPlayers()) if (p != null && p.GetPlayerID() == playerId) return p.GetPlayerName();
            if (ZNet.instance != null && ZDOMan.instance != null)
                foreach (var info in ZNet.instance.GetPlayerList())
                {
                    var pz = ZDOMan.instance.GetZDO(info.m_characterID);
                    if (pz != null && pz.GetLong(ZDOVars.s_playerID, 0L) == playerId && !string.IsNullOrEmpty(info.m_name)) return info.m_name;
                }
            int idx = ward != null ? ward.GetInt(ZDOVars.s_creatorIndex, -1) : -1;
            var history = ZNet.World?.m_playerHistory;
            if (history != null && idx >= 0 && idx < history.Count && !string.IsNullOrEmpty(history[idx].m_displayName)) return history[idx].m_displayName;
            return "Viking";
        }

        public static List<ZDO> AllWardZdos()
        {
            var all = new List<ZDO>(); int index = 0;
            while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(Plugin.PrefabName, all, ref index)) { }
            return all;
        }

        static void Broadcast()
        {
            var online = OnlinePlayerIds();
            var pkg = new ZPackage();
            pkg.Write(online.Count);
            foreach (var id in online) pkg.Write(id);
            var wards = AllWardZdos();
            pkg.Write(wards.Count);
            foreach (var z in wards)
            {
                var state = (ChallengeState)z.GetInt(HState, 0);
                bool paused = z.GetBool(WardChallenge.HPaused, false);
                long creator = z.GetLong(ZDOVars.s_creator);
                float remaining = 0f; bool running = false;
                switch (state)
                {
                    case ChallengeState.Idle:
                    case ChallengeState.Countdown:
                        remaining = Cfg.CountdownMinutes.Value * 60f - z.GetFloat(WardChallenge.HElapsed, 0f);
                        running = !paused && online.Contains(creator); break;   // builder online: counts loaded or not
                    case ChallengeState.Resting:
                        remaining = z.GetFloat(WardChallenge.HRestFor, 0f) - z.GetFloat(WardChallenge.HRestElapsed, 0f);
                        running = !paused && online.Contains(creator); break;   // builder online: counts loaded or not
                    case ChallengeState.Active:
                        remaining = Cfg.WaveTimeLimitMinutes.Value * 60f - z.GetFloat(WardChallenge.HWaveElapsed, 0f);
                        running = IsSimulated(z.GetPosition()); break;
                }
                pkg.Write(z.m_uid); pkg.Write(creator); pkg.Write(NameOf(creator, z)); pkg.Write((int)state);
                pkg.Write(paused); pkg.Write(running); pkg.Write(Mathf.Max(0f, remaining)); pkg.Write(z.GetPosition());
            }
            pkg.SetPos(0);
            RPC_Wards(0L, pkg);                                                     // the server shows the HUD too (host / single-player)
            pkg.SetPos(0);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Rpc, pkg);
        }

        // ---- every peer: keep the latest summary ----
        static void RPC_Wards(long sender, ZPackage pkg)
        {
            var online = new HashSet<long>();
            int o = pkg.ReadInt();
            for (int i = 0; i < o; i++) online.Add(pkg.ReadLong());
            Online = online;
            Entries.Clear();
            int n = pkg.ReadInt();
            for (int i = 0; i < n; i++)
            {
                Entries.Add(new Entry
                {
                    Id = pkg.ReadZDOID(), Creator = pkg.ReadLong(), Name = pkg.ReadString(), State = (ChallengeState)pkg.ReadInt(),
                    Paused = pkg.ReadBool(), Running = pkg.ReadBool(), Remaining = pkg.ReadSingle(), Pos = pkg.ReadVector3(), ReceivedAt = Time.time
                });
            }
            LastUpdate = Time.time;
        }

        public static bool IsRunning(ChallengeState s) => s == ChallengeState.Idle || s == ChallengeState.Countdown || s == ChallengeState.Active || s == ChallengeState.Resting;

        public static string Describe() =>
            string.Join("\n", Entries.Select(e => $"{e.Id} creator={e.Creator} name={e.Name} state={e.State} paused={e.Paused} running={e.Running} remaining={e.RemainingNow:0} pos={e.Pos}")) + $"\ncount={Entries.Count} age={(LastUpdate < 0 ? -1 : Time.time - LastUpdate):0.0}";
    }
}
