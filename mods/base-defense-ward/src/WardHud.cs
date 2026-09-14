using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BaseDefenseWard
{
    // Small panel left of the minimap: ward icon + the countdown that matters right now
    // (build deadline, time to next wave, or wave status). Reads synced ZDO data, so it works for any peer.
    public class WardHud : MonoBehaviour
    {
        public static WardHud Instance;
        static readonly int HState = "bdw_state".GetStableHashCode();

        RectTransform _root;
        Image _icon;
        TMP_Text _text;
        float _scanTimer;
        ZDO _myWard;
        public string CurrentText => _text != null ? _text.text : "";
        public bool Visible => _root != null && _root.gameObject.activeSelf;

        public static void Create(Hud hud)
        {
            if (Instance != null || hud == null || hud.m_rootObject == null) return;
            // The component lives on an always-active holder; the visible panel is a child that gets toggled,
            // otherwise disabling the panel would also stop this Update().
            var holder = new GameObject("BaseDefenseWardHud", typeof(RectTransform));
            // Sit next to the minimap in its own parent so anchors/scale match it exactly.
            var minimapRoot = Minimap.instance?.m_smallRoot?.GetComponent<RectTransform>();
            holder.transform.SetParent(minimapRoot != null ? minimapRoot.parent : hud.m_rootObject.transform, false);
            var hr = holder.GetComponent<RectTransform>();
            hr.anchorMin = new Vector2(0f, 0f); hr.anchorMax = new Vector2(1f, 1f); hr.offsetMin = hr.offsetMax = Vector2.zero;
            Instance = holder.AddComponent<WardHud>();
            Instance.Build(hud);
        }

        void Build(Hud hud)
        {
            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(transform, false);
            _root = panel.GetComponent<RectTransform>();
            _root.anchorMin = _root.anchorMax = new Vector2(0f, 0f);
            _root.pivot = new Vector2(1f, 1f);
            _root.sizeDelta = new Vector2(190f, 36f);
            Reposition();

            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.45f);

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(_root, false);
            var ir = iconGo.GetComponent<RectTransform>();
            ir.anchorMin = ir.anchorMax = new Vector2(0f, 0.5f); ir.pivot = new Vector2(0f, 0.5f);
            ir.sizeDelta = new Vector2(32f, 32f); ir.anchoredPosition = new Vector2(4f, 0f);
            _icon = iconGo.AddComponent<Image>();
            _icon.sprite = Plugin.WardPrefab?.GetComponent<Piece>()?.m_icon;
            _icon.preserveAspect = true;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(_root, false);
            var tr = textGo.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0f, 0f); tr.anchorMax = new Vector2(1f, 1f);
            tr.offsetMin = new Vector2(42f, 2f); tr.offsetMax = new Vector2(-6f, -2f);
            _text = textGo.AddComponent<TextMeshProUGUI>();
            var template = hud.m_healthText;
            if (template != null) { _text.font = template.font; _text.fontSharedMaterial = template.fontSharedMaterial; }
            _text.fontSize = 17f;
            _text.color = Color.white;
            _text.alignment = TextAlignmentOptions.MidlineLeft;
            _text.enableWordWrapping = false;
            _text.overflowMode = TextOverflowModes.Ellipsis;
            panel.SetActive(false);
        }

        // Put the panel's top-right corner 12 px left of the minimap's top-left corner (in the shared parent's space).
        void Reposition()
        {
            var mm = Minimap.instance?.m_smallRoot?.GetComponent<RectTransform>();
            var holder = (RectTransform)transform;
            if (mm == null || _root == null) { _root.anchoredPosition = new Vector2(holder.rect.width - 280f, holder.rect.height - 14f); return; }
            var corners = new Vector3[4];
            mm.GetWorldCorners(corners);                       // 1 = top-left
            var local = holder.InverseTransformPoint(corners[1]);
            var fromBottomLeft = new Vector2(local.x, local.y) + holder.rect.size * holder.pivot;
            _root.anchoredPosition = fromBottomLeft + new Vector2(-12f, 0f);
        }

        void Update()
        {
            if (_root == null || Player.m_localPlayer == null || ZNet.instance == null || ZDOMan.instance == null) { Hide(); return; }
            _scanTimer -= Time.deltaTime;
            if (_scanTimer <= 0f)
            {
                _scanTimer = 2f; _myWard = FindMyWard(); Reposition();
                if (_icon.sprite == null) _icon.sprite = Plugin.WardPrefab?.GetComponent<Piece>()?.m_icon; // prefab registers after Hud.Awake
            }

            double now = ZNet.instance.GetTimeSeconds();
            string line = null;
            if (_myWard != null)
            {
                var state = (ChallengeState)_myWard.GetInt(HState, 0);
                switch (state)
                {
                    case ChallengeState.Idle:
                    case ChallengeState.Countdown:
                    {
                        double left = Cfg.CountdownMinutes.Value * 60.0 - _myWard.GetFloat(WardChallenge.HElapsed, 0f);
                        line = "Wave in " + Fmt(left); break;
                    }
                    case ChallengeState.Active:
                    {
                        double left = Cfg.WaveTimeLimitMinutes.Value * 60.0 - _myWard.GetFloat(WardChallenge.HWaveElapsed, 0f);
                        line = "Wave! hold " + Fmt(left); break;
                    }
                    case ChallengeState.Won: line = "Ward defended"; break;
                    case ChallengeState.Lost: line = "Ward lost"; break;
                }
            }
            else if (Cfg.BuildDeadlineMinutes.Value > 0f && !Progress.Has(Progress.BuiltKey) && !Progress.Has(Progress.DeadlineFailedKey)
                     && ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(Progress.WorldStartKey, out var s) && long.TryParse(s, out var start))
            {
                line = "Build ward in " + Fmt(Cfg.BuildDeadlineMinutes.Value * 60.0 - (now - start));
            }

            if (line == null) { Hide(); return; }
            if (!_root.gameObject.activeSelf) _root.gameObject.SetActive(true);
            _text.text = line;
        }

        void Hide() { if (_root != null && _root.gameObject.activeSelf) _root.gameObject.SetActive(false); }

        ZDO FindMyWard()
        {
            long me = Player.m_localPlayer.GetPlayerID();
            var all = new List<ZDO>(); int index = 0;
            while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(Plugin.PrefabName, all, ref index)) { }
            ZDO best = null;
            foreach (var z in all)
            {
                if (z.GetLong(ZDOVars.s_creator) != me) continue;
                // Prefer a running challenge over a finished one.
                int s = z.GetInt(HState, 0);
                bool running = s == (int)ChallengeState.Countdown || s == (int)ChallengeState.Active || s == (int)ChallengeState.Idle;
                if (best == null || (running && !IsRunning(best))) best = z;
            }
            return best;
        }

        static bool IsRunning(ZDO z) { int s = z.GetInt(HState, 0); return s <= (int)ChallengeState.Active; }

        static string Fmt(double seconds)
        {
            if (seconds < 0) seconds = 0;
            var t = TimeSpan.FromSeconds(seconds);
            return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes:00}:{t.Seconds:00}";
        }
    }

    [HarmonyPatch(typeof(Hud), "Awake")]
    static class Hud_Awake { static void Postfix(Hud __instance) => WardHud.Create(__instance); }
}
