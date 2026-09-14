using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace BaseDefenseWard
{
    public enum ChallengeState { Idle = 0, Countdown = 1, Active = 2, Won = 3, Lost = 4, Cancelled = 5 }

    // Attached to the ward prefab. Logic runs only on the ZDO owner (the server in multiplayer).
    public class WardChallenge : MonoBehaviour
    {
        static readonly int HState = "bdw_state".GetStableHashCode();
        // Timers accumulate only while the ward is simulated (owner ticking), so they pause when nobody is online or nearby.
        public static readonly int HElapsed = "bdw_elapsed".GetStableHashCode();
        public static readonly int HWaveElapsed = "bdw_waveelapsed".GetStableHashCode();
        static readonly int HTier = "bdw_tier".GetStableHashCode();
        static readonly int HMobCount = "bdw_mobcount".GetStableHashCode();
        public static readonly int HMobWard = "bdw_ward".GetStableHashCode(); // set on spawned mobs: owning ward id

        static readonly System.Random Rng = new System.Random();
        public static readonly List<WardChallenge> All = new List<WardChallenge>();

        ZNetView _nview;
        WearNTear _wnt;
        readonly Queue<string> _toSpawn = new Queue<string>();
        float _spawnTimer;
        string _id;
        double _lastTick = -1;

        void Awake()
        {
            _nview = GetComponent<ZNetView>();
            if (_nview == null || !_nview.IsValid()) return;
            _wnt = GetComponent<WearNTear>();
            if (_wnt != null) _wnt.m_onDestroyed = (Action)Delegate.Combine(_wnt.m_onDestroyed, new Action(OnDestroyed));
            _id = _nview.GetZDO().m_uid.ToString();
            All.Add(this);
            InvokeRepeating(nameof(Tick), 1f, 1f);
        }

        void OnDestroy() => All.Remove(this);

        // ---- state ----
        public ChallengeState State => (ChallengeState)_nview.GetZDO().GetInt(HState, 0);
        void SetState(ChallengeState s) => _nview.GetZDO().Set(HState, (int)s);
        double Now => ZNet.instance.GetTimeSeconds();
        public int Tier => _nview.GetZDO().GetInt(HTier, 0);
        public float Elapsed => _nview.GetZDO().GetFloat(HElapsed, 0f);
        public float WaveElapsed => _nview.GetZDO().GetFloat(HWaveElapsed, 0f);

        public string Status()
        {
            var z = _nview.GetZDO();
            return $"state={State} tier={Tier} owner={_nview.IsOwner()} hp={(_wnt != null ? _wnt.GetHealthPercentage() * 100f : -1f):0}% elapsed={Elapsed:0}s " +
                   $"waveElapsed={WaveElapsed:0}s alive={AliveMobs().Count} pending={_toSpawn.Count} planned={z.GetInt(HMobCount)}";
        }

        void Tick()
        {
            if (!_nview.IsValid() || !_nview.IsOwner() || ZNet.instance == null) { _lastTick = -1; return; }
            var z = _nview.GetZDO();
            // Only time that passed while this owner was ticking counts. A gap (relog, nobody nearby, server
            // restart, ownership change) adds nothing, so timers pause instead of expiring while players are away.
            float dt = _lastTick < 0 ? 0f : Mathf.Clamp((float)(Now - _lastTick), 0f, 2f);
            _lastTick = Now;
            switch (State)
            {
                case ChallengeState.Idle:
                    z.Set(HElapsed, 0f);
                    SetState(ChallengeState.Countdown);
                    if (!Progress.Has(Progress.BuiltKey)) ZoneSystem.instance.SetGlobalKey(Progress.BuiltKey);
                    Say($"Base Defense Ward armed. The wave arrives in {Cfg.CountdownMinutes.Value:0.#} minutes.");
                    break;

                case ChallengeState.Countdown:
                    z.Set(HElapsed, Elapsed + dt);
                    if (Elapsed >= Cfg.CountdownMinutes.Value * 60f) StartWave();
                    break;

                case ChallengeState.Active:
                    z.Set(HWaveElapsed, WaveElapsed + dt);
                    PumpSpawns();
                    var alive = AliveMobs();
                    DirectMobs(alive);
                    bool timeUp = WaveElapsed >= Cfg.WaveTimeLimitMinutes.Value * 60f;
                    if (_toSpawn.Count == 0 && alive.Count == 0) Win("Wave destroyed");
                    else if (timeUp) { Despawn(alive); Win("Ward held until dawn"); }
                    break;
            }
        }

        void StartWave()
        {
            var z = _nview.GetZDO();
            int tier = Progress.BossTier(), completed = Progress.Completed();
            int count = Mathf.Clamp(Cfg.BaseMobCount.Value + Cfg.MobsPerCompletedChallenge.Value * completed, 1, Cfg.MaxMobCount.Value);
            _toSpawn.Clear();
            foreach (var p in MobPool.Sample(count, tier, completed, Rng)) _toSpawn.Enqueue(p);
            z.Set(HTier, tier);
            z.Set(HMobCount, _toSpawn.Count);
            z.Set(HWaveElapsed, 0f);
            SetState(ChallengeState.Active);
            Plugin.Log.LogInfo($"Wave start: ward={_id} tier={tier} completed={completed} mobs={string.Join(",", _toSpawn)}");
            Say("The wave is here! Defend the ward!");
        }

        void PumpSpawns()
        {
            if (_toSpawn.Count == 0) return;
            _spawnTimer -= 1f;
            if (_spawnTimer > 0f) return;
            _spawnTimer = Cfg.SpawnIntervalSeconds.Value;
            Spawn(_toSpawn.Dequeue());
        }

        void Spawn(string prefabName)
        {
            var prefab = ZNetScene.instance.GetPrefab(prefabName);
            if (prefab == null) { Plugin.Log.LogWarning($"Missing mob prefab {prefabName}"); return; }
            Vector3 pos = transform.position;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                float a = (float)(Rng.NextDouble() * Math.PI * 2);
                var candidate = transform.position + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * Cfg.SpawnRadius.Value;
                if (ZoneSystem.instance.GetGroundHeight(candidate, out var h) && h > ZoneSystem.instance.m_waterLevel) { candidate.y = h + 0.3f; pos = candidate; break; }
            }
            var go = Instantiate(prefab, pos, Quaternion.LookRotation((transform.position - pos).normalized));
            var nv = go.GetComponent<ZNetView>();
            nv?.GetZDO()?.Set(HMobWard, _id);
            var ai = go.GetComponent<MonsterAI>();
            if (ai != null)
            {
                ai.SetHuntPlayer(true);
                ai.Alert();
                nv?.GetZDO()?.Set(ZDOVars.s_despawnInDay, false);
            }
            Plugin.Log.LogInfo($"Spawned {prefabName} at {pos}");
        }

        public List<Character> AliveMobs()
        {
            var list = new List<Character>();
            foreach (var c in Character.GetAllCharacters())
            {
                if (c == null || c.IsDead() || c.IsPlayer()) continue;
                var nv = c.GetComponent<ZNetView>();
                if (nv != null && nv.IsValid() && nv.GetZDO().GetString(HMobWard) == _id) list.Add(c);
            }
            return list;
        }

        // "Not stupid": hunt any exposed player nearby, otherwise march on the ward.
        void DirectMobs(List<Character> mobs)
        {
            var target = GetComponent<StaticTarget>();
            var players = Player.GetAllPlayers();
            foreach (var c in mobs)
            {
                var ai = c.GetComponent<MonsterAI>();
                if (ai == null) continue;
                bool exposed = players.Any(p => !p.IsDead() && !p.InShelter() &&
                                                Vector3.Distance(p.transform.position, c.transform.position) <= Cfg.PlayerHuntRange.Value);
                ai.SetHuntPlayer(exposed);
                if (!exposed && target != null)
                {
                    // Point the AI at the ward directly so it doesn't need line of sight to pick it up.
                    var cur = AccessTools.Field(typeof(MonsterAI), "m_targetStatic").GetValue(ai) as StaticTarget;
                    var creature = AccessTools.Field(typeof(MonsterAI), "m_targetCreature").GetValue(ai) as Character;
                    if (cur == null && creature == null) AccessTools.Field(typeof(MonsterAI), "m_targetStatic").SetValue(ai, target);
                    if (!ai.IsAlerted()) ai.Alert();
                }
            }
        }

        void Despawn(List<Character> mobs)
        {
            foreach (var c in mobs) { var nv = c.GetComponent<ZNetView>(); if (nv != null && nv.IsValid()) nv.Destroy(); }
        }

        void Win(string why)
        {
            SetState(ChallengeState.Won);
            int completed = Progress.Completed();
            var reward = MobPool.RewardFor(Tier, completed);
            Progress.SetCompleted(completed + 1);
            foreach (var (prefab, amount) in reward) DropReward(prefab, amount);
            Plugin.Log.LogInfo($"Challenge won ({why}): ward={_id} completed->{completed + 1} reward={string.Join(",", reward.Select(r => r.prefab + "x" + r.amount))}");
            Say($"Ward defended! {why}. Rewards have been dropped at the ward.");
        }

        void DropReward(string prefab, int amount)
        {
            var prefabGo = ZNetScene.instance.GetPrefab(prefab);
            var item = prefabGo?.GetComponent<ItemDrop>();
            if (item == null) return;
            item.m_itemData.m_dropPrefab = prefabGo; // null on never-instantiated prefabs; DropItem needs it
            int maxStack = Mathf.Max(1, item.m_itemData.m_shared.m_maxStackSize);
            while (amount > 0)
            {
                int n = Mathf.Min(amount, maxStack); amount -= n;
                var offset = new Vector3((float)Rng.NextDouble() * 2f - 1f, 1.5f, (float)Rng.NextDouble() * 2f - 1f);
                ItemDrop.DropItem(item.m_itemData.Clone(), n, transform.position + offset, Quaternion.identity);
            }
        }

        void OnDestroyed()
        {
            if (!_nview.IsValid() || !_nview.IsOwner()) return;
            var s = State;
            if (s == ChallengeState.Active)
            {
                SetState(ChallengeState.Lost);
                Despawn(AliveMobs());
                Plugin.Log.LogInfo($"Challenge lost: ward={_id} destroyed during wave");
                Say("The ward has fallen. Challenge lost.");
                if (Cfg.DeleteWorldOnLoss.Value) WorldReset.Schedule("ward destroyed");
            }
            else if (s == ChallengeState.Countdown)
            {
                Plugin.Log.LogInfo($"Challenge cancelled: ward={_id} removed before the wave");
            }
        }

        public static void Say(string text)
        {
            if (MessageHud.instance != null) MessageHud.instance.MessageAll(MessageHud.MessageType.Center, text);
        }

        // Nearest ward, preferring ones whose challenge is still running over finished ones.
        public static WardChallenge Nearest(Vector3 pos, float r = 500f) =>
            All.Where(w => w != null && Vector3.Distance(w.transform.position, pos) <= r)
               .OrderBy(w => w.State == ChallengeState.Countdown || w.State == ChallengeState.Active || w.State == ChallengeState.Idle ? 0 : 1)
               .ThenBy(w => Vector3.Distance(w.transform.position, pos)).FirstOrDefault();
    }
}
