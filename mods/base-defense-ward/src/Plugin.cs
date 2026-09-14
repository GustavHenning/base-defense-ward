using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace BaseDefenseWard
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "com.night.basedefenseward";
        public const string NAME = "Base Defense Ward";
        public const string VERSION = "0.1.2";

        public const string PrefabName = "BaseDefenseWard";
        public const string SourcePrefab = "guard_stone";

        public static readonly Color GlowColor = new Color(0.9f, 0.35f, 0.4f);
        public static ManualLogSource Log;
        public static GameObject WardPrefab;
        private static GameObject _holder;

        public static Plugin Instance;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            Cfg.Bind(Config);
            Log.LogInfo($"{NAME} {VERSION} loading");
            _holder = new GameObject("BaseDefenseWard_PrefabHolder");
            _holder.SetActive(false);
            DontDestroyOnLoad(_holder);
            new Harmony(GUID).PatchAll(Assembly.GetExecutingAssembly());
        }

        private void Update()
        {
            Deadline.Tick(Time.deltaTime);
            WardBoard.Tick(Time.deltaTime);
            WardPins.Tick(Time.deltaTime);
            Interaction.Tick();
        }

        // Entry point for external tooling (dev harness calls this via reflection). Returns a text report.
        public static string DebugCommand(string line)
        {
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var cmd = parts.Length > 0 ? parts[0] : "";
            var p = Player.m_localPlayer;
            switch (cmd)
            {
                case "status":
                {
                    var w = p != null ? WardChallenge.Nearest(p.transform.position) : WardChallenge.All.FirstOrDefault();
                    return (w == null ? "no ward nearby" : w.Status()) + "\n" + Progress.Summary() + "\n" + Deadline.Status();
                }
                case "skip": // skip <seconds>: fast-forward the current timer of this ward
                {
                    var w = WardChallenge.Nearest(p.transform.position);
                    if (w == null) return "no ward nearby";
                    long s = long.Parse(parts[1]);
                    var z = w.GetComponent<ZNetView>().GetZDO();
                    int hs = w.State == ChallengeState.Active ? WardChallenge.HWaveElapsed : WardChallenge.HElapsed; z.Set(hs, z.GetFloat(hs, 0f) + s);
                    return "skipped " + s;
                }
                case "mobs":
                {
                    var w = WardChallenge.Nearest(p.transform.position);
                    if (w == null) return "no ward nearby";
                    return string.Join("\n", w.AliveMobs().Select(c => $"{c.gameObject.name}\t{c.GetHealth():0}/{c.GetMaxHealth():0}\t{Vector3.Distance(c.transform.position, w.transform.position):0}m hunt={c.GetComponent<MonsterAI>()?.HuntPlayer()}")) + $"\ncount={w.AliveMobs().Count}";
                }
                case "kill": // kill every mob of the nearest ward's wave, wherever they are
                {
                    var w = WardChallenge.Nearest(p.transform.position);
                    if (w == null) return "no ward nearby";
                    int n = 0;
                    foreach (var c in w.AliveMobs()) { var hit = new HitData(); hit.m_damage.m_damage = 1e10f; c.Damage(hit); n++; }
                    return "killed " + n;
                }
                case "setcompleted": Progress.SetCompleted(int.Parse(parts[1])); return Progress.Summary();
                case "setkey": ZoneSystem.instance.SetGlobalKey(parts[1]); return Progress.Summary();
                case "removekey": ZoneSystem.instance.RemoveGlobalKey(parts[1]); return Progress.Summary();
                case "sample": return string.Join(",", MobPool.Sample(int.Parse(parts[1]), int.Parse(parts[2]), int.Parse(parts[3]), new System.Random()));
                case "reward": return string.Join(",", MobPool.RewardFor(int.Parse(parts[1]), int.Parse(parts[2])).Select(r => r.prefab + "x" + r.amount));
                case "cfg":
                    if (parts.Length == 3) { var e = Instance.Config.Where(kv => kv.Key.Key == parts[1]).Select(kv => kv.Value).FirstOrDefault(); if (e == null) return "no such key"; e.SetSerializedValue(parts[2]); }
                    return string.Join("\n", Instance.Config.Select(kv => $"{kv.Key.Key}={kv.Value.GetSerializedValue()}"));
                case "hud": return WardHud.Instance == null ? "no hud" : $"visible={WardHud.Instance.Visible} rows={WardHud.Instance.RowCount} tops={string.Join(",", WardHud.Instance.RowTops.Select(t => t.ToString("0")))} text={WardHud.Instance.CurrentText}";
                case "pins": return string.Join("\n", WardPins.Describe()) + $"\ncount={WardPins.Count}";
                case "board": return WardBoard.Describe();
                case "pause": return Interaction.TogglePause(p) ?? "toggled";   // same path as the hotkey
                case "startnow": { var w = WardChallenge.Nearest(p.transform.position); return w == null ? "no ward nearby" : (Interaction.StartNow(w) ?? "not in countdown"); }
                case "hover": { var w = WardChallenge.Nearest(p.transform.position); return w == null ? "no ward nearby" : w.GetComponent<PrivateArea>().GetHoverText().Replace("\n", " / "); }
                case "interact": // what pressing E on the nearest ward does, plus its hover text
                {
                    var w = WardChallenge.Nearest(p.transform.position);
                    if (w == null) return "no ward nearby";
                    var area = w.GetComponent<PrivateArea>();
                    return "result=" + area.Interact(p, false, false) + " hover=" + area.GetHoverText().Replace("\n", " / ");
                }
                case "setcreator": // setcreator <playerId>: pretend the nearest ward was built by another player (multiplayer HUD/pin tests)
                {
                    var w = WardChallenge.Nearest(p.transform.position);
                    if (w == null) return "no ward nearby";
                    var z = w.GetComponent<ZNetView>().GetZDO();
                    z.Set(ZDOVars.s_creator, long.Parse(parts[1])); z.Set(ZDOVars.s_creatorIndex, -1);
                    return $"creator={z.GetLong(ZDOVars.s_creator)}";
                }
                case "hudrects": // screen rectangles of our rows and the vanilla HUD elements they must not overlap
                {
                    var hud = Hud.instance; var mh = MessageHud.instance;
                    var items = new List<(string, RectTransform)>
                    {
                        ("minimap", Minimap.instance?.m_smallRoot?.GetComponent<RectTransform>()),
                        ("statusEffects", hud?.m_statusEffectListRoot), ("eventBar", hud?.m_eventBar?.GetComponent<RectTransform>()),
                        ("messageTopLeft", mh?.m_messageText?.rectTransform), ("messageCenter", mh?.m_messageCenterText?.rectTransform),
                        ("healthPanel", hud?.m_healthPanel), ("guardianPower", hud?.m_gpRoot), ("buildHud", hud?.m_buildHud?.GetComponent<RectTransform>()),
                        ("saveIcon", hud?.m_saveIcon?.GetComponent<RectTransform>()), ("betaText", hud?.m_betaText?.GetComponent<RectTransform>()),
                    };
                    var rows = WardHud.Instance != null ? WardHud.Instance.RowRects().ToList() : new List<RectTransform>();
                    for (int i = 0; i < rows.Count; i++) items.Add(("row" + i, rows[i]));
                    Rect R(RectTransform rt) { var c = new Vector3[4]; rt.GetWorldCorners(c); return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y); }
                    var sb = new System.Text.StringBuilder();
                    foreach (var (name, rt) in items)
                    {
                        if (rt == null) { sb.AppendLine($"{name}\tnull"); continue; }
                        var r = R(rt);
                        var overlaps = rows.Where(row => row != rt && row.gameObject.activeInHierarchy && r.Overlaps(R(row))).Select(row => row.name);
                        sb.AppendLine($"{name}\tactive={rt.gameObject.activeInHierarchy}\tx={r.xMin:0}-{r.xMax:0}\ty={r.yMin:0}-{r.yMax:0}\toverlapsRows={string.Join(",", overlaps)}");
                    }
                    return sb.ToString().TrimEnd();
                }
                case "wards": // every ward ZDO of the local player, loaded or not
                    return string.Join("\n", OnePerPlayer.WardsOf(p.GetPlayerID()).Select(z => $"{z.m_uid} state={(ChallengeState)z.GetInt("bdw_state".GetStableHashCode())} pos={z.GetPosition()}")) + $"\ncount={OnePerPlayer.WardsOf(p.GetPlayerID()).Count} removed={OnePerPlayer.RemovedCount} blocked={OnePerPlayer.BlockedCount}";
                case "reset": WorldReset.Schedule("debug"); return "scheduled";
                default: return "unknown: status skip <s> mobs setcompleted <n> setkey <k> removekey <k> sample <n> <tier> <completed> reward <tier> <completed> cfg [key value] reset";
            }
        }


        // ---- Prefab registration -------------------------------------------------

        public static void TryRegister()
        {
            var zns = ZNetScene.instance;
            var odb = ObjectDB.instance;
            if (zns == null || odb == null) return;
            if (odb.m_items == null || odb.m_items.Count == 0) return; // main-menu ObjectDB is empty

            if (WardPrefab == null || zns.GetPrefab(PrefabName) == null)
            {
                var src = zns.GetPrefab(SourcePrefab);
                if (src == null) { Log.LogError($"Source prefab {SourcePrefab} not found"); return; }
                WardPrefab = UnityEngine.Object.Instantiate(src, _holder.transform);
                WardPrefab.name = PrefabName;

                var piece = WardPrefab.GetComponent<Piece>();
                piece.m_name = "Base Defense Ward";
                piece.m_description = "Build it and a wave comes for it. Defend it to earn rewards.";
                // Build cost and workbench requirement are inherited from the vanilla ward (guard_stone).
                WardPrefab.AddComponent<WardChallenge>();
                var st = WardPrefab.AddComponent<StaticTarget>();   // lets wave mobs path to and attack the ward itself
                st.m_primaryTarget = true;
                st.m_randomTarget = true;
                var area = WardPrefab.GetComponent<PrivateArea>();
                if (area != null)
                {
                    area.m_name = "Base Defense Ward";
                    // Glow: a point light that lives under the enabled-effect object, so it only
                    // shows while the ward is activated (PrivateArea toggles that object on/off).
                    if (area.m_enabledEffect != null)
                    {
                        foreach (var l in area.m_enabledEffect.GetComponentsInChildren<Light>(true)) { l.color = GlowColor; }
                        foreach (var ps in area.m_enabledEffect.GetComponentsInChildren<ParticleSystem>(true))
                        {
                            var main = ps.main; main.startColor = GlowColor;
                            var col = ps.colorOverLifetime; if (col.enabled) col.color = new ParticleSystem.MinMaxGradient(GlowColor);
                        }
                        foreach (var r in area.m_enabledEffect.GetComponentsInChildren<Renderer>(true))
                            foreach (var m in r.materials) // instanced copies, so the vanilla ward is untouched
                            {
                                if (m == null) continue;
                                if (m.HasProperty("_Color")) m.SetColor("_Color", GlowColor);
                                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", GlowColor * 2f);
                            }
                        var glow = new GameObject("BaseDefenseWardGlow");
                        glow.transform.SetParent(area.m_enabledEffect.transform, false);
                        glow.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                        var light = glow.AddComponent<Light>();
                        light.type = LightType.Point;
                        light.color = GlowColor;
                        light.intensity = 1.2f;
                        light.range = 6f;
                        light.shadows = LightShadows.None;
                    }
                }

                zns.m_prefabs.Add(WardPrefab);
                var named = AccessTools.Field(typeof(ZNetScene), "m_namedPrefabs").GetValue(zns) as Dictionary<int, GameObject>;
                named[PrefabName.GetStableHashCode()] = WardPrefab;
                Log.LogInfo($"Registered prefab {PrefabName} (clone of {SourcePrefab})");
            }

            var hammer = odb.GetItemPrefab("Hammer");
            var table = hammer?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_buildPieces;
            if (table == null) { Log.LogError("Hammer piece table not found"); return; }
            if (!table.m_pieces.Contains(WardPrefab))
            {
                table.m_pieces.Add(WardPrefab);
                Log.LogInfo("Added Base Defense Ward to Hammer piece table");
            }
        }
    }

    // Tint the ward emission on our clone whenever its status refreshes (vanilla ward keeps its own colour
    // because material instances are per-object).
    [HarmonyPatch(typeof(PrivateArea), "UpdateStatus")]
    static class PrivateArea_UpdateStatus
    {
        static void Postfix(PrivateArea __instance)
        {
            if (!__instance.name.StartsWith(Plugin.PrefabName) || __instance.m_model == null) return;
            foreach (var mat in __instance.m_model.materials)
            {
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Plugin.GlowColor * 2f);
            }
        }
    }

    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    static class ZNetScene_Awake { static void Postfix() => Plugin.TryRegister(); }

    [HarmonyPatch(typeof(ObjectDB), "Awake")]
    static class ObjectDB_Awake { static void Postfix() => Plugin.TryRegister(); }

    [HarmonyPatch(typeof(ObjectDB), "CopyOtherDB")]
    static class ObjectDB_CopyOtherDB { static void Postfix() => Plugin.TryRegister(); }

}
