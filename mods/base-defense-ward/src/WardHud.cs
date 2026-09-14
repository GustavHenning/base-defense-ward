using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BaseDefenseWard
{
    // Panel list under the minimap: one row per ward that matters right now, stacked vertically so several
    // players' timers never overlap. Rows: your own ward (any state), every other player's running ward, or
    // the build deadline when nobody has built yet. Data comes from WardBoard, so it works at any distance.
    public class WardHud : MonoBehaviour
    {
        public static WardHud Instance;
        const float RowWidth = 300f, RowHeight = 36f, RowGap = 4f;

        class Row { public GameObject Go; public RectTransform Rect; public Image Icon; public TMP_Text Text; }

        RectTransform _holder;
        readonly List<Row> _rows = new List<Row>();
        readonly List<string> _lines = new List<string>();
        Sprite _sprite;
        Vector2 _anchor;              // top-right corner of the first row
        float _scanTimer;
        TMP_Text _template;

        public string CurrentText => string.Join(" | ", _lines);
        public int RowCount => _lines.Count;
        public bool Visible => _lines.Count > 0;
        public IEnumerable<float> RowTops => _rows.Take(_lines.Count).Select(r => r.Rect.anchoredPosition.y);
        public IEnumerable<RectTransform> RowRects() => _rows.Take(_lines.Count).Select(r => r.Rect);

        public static void Create(Hud hud)
        {
            if (Instance != null || hud == null || hud.m_rootObject == null) return;
            // The component lives on an always-active holder; the rows are children that get toggled,
            // otherwise disabling a row would also stop this Update().
            var holder = new GameObject("BaseDefenseWardHud", typeof(RectTransform));
            // Sit next to the minimap in its own parent so anchors/scale match it exactly.
            var minimapRoot = Minimap.instance?.m_smallRoot?.GetComponent<RectTransform>();
            holder.transform.SetParent(minimapRoot != null ? minimapRoot.parent : hud.m_rootObject.transform, false);
            var hr = holder.GetComponent<RectTransform>();
            hr.anchorMin = new Vector2(0f, 0f); hr.anchorMax = new Vector2(1f, 1f); hr.offsetMin = hr.offsetMax = Vector2.zero;
            Instance = holder.AddComponent<WardHud>();
            Instance._holder = hr;
            Instance._template = hud.m_healthText;
            Instance.Reposition();
        }

        Row MakeRow()
        {
            var panel = new GameObject("Row" + _rows.Count, typeof(RectTransform));
            panel.transform.SetParent(transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(RowWidth, RowHeight);
            panel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(rect, false);
            var ir = iconGo.GetComponent<RectTransform>();
            ir.anchorMin = ir.anchorMax = new Vector2(0f, 0.5f); ir.pivot = new Vector2(0f, 0.5f);
            ir.sizeDelta = new Vector2(32f, 32f); ir.anchoredPosition = new Vector2(4f, 0f);
            var icon = iconGo.AddComponent<Image>();
            icon.preserveAspect = true;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(rect, false);
            var tr = textGo.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0f, 0f); tr.anchorMax = new Vector2(1f, 1f);
            tr.offsetMin = new Vector2(42f, 2f); tr.offsetMax = new Vector2(-6f, -2f);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            if (_template != null) { text.font = _template.font; text.fontSharedMaterial = _template.fontSharedMaterial; }
            text.fontSize = 16f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;

            var row = new Row { Go = panel, Rect = rect, Icon = icon, Text = text };
            _rows.Add(row);
            return row;
        }

        // First row's top-right corner sits 8 px under the minimap's bottom-right corner (in the shared parent's
        // space). The band left of the minimap belongs to the vanilla status-effect icons, so rows go below it.
        void Reposition()
        {
            var mm = Minimap.instance?.m_smallRoot?.GetComponent<RectTransform>();
            if (mm == null) { _anchor = new Vector2(_holder.rect.width - 20f, _holder.rect.height - 160f); return; }
            var corners = new Vector3[4];
            mm.GetWorldCorners(corners);                       // 3 = bottom-right
            var local = _holder.InverseTransformPoint(corners[3]);
            var fromBottomLeft = new Vector2(local.x, local.y) + _holder.rect.size * _holder.pivot;
            _anchor = fromBottomLeft + new Vector2(0f, -8f);
        }

        void Update()
        {
            _lines.Clear();
            if (Player.m_localPlayer != null && ZNet.instance != null)
            {
                _scanTimer -= Time.deltaTime;
                if (_scanTimer <= 0f)
                {
                    _scanTimer = 2f; Reposition();
                    if (_sprite == null) _sprite = Plugin.WardPrefab?.GetComponent<Piece>()?.m_icon; // prefab registers after Hud.Awake
                }
                BuildLines();
            }

            for (int i = 0; i < _lines.Count; i++)
            {
                var row = i < _rows.Count ? _rows[i] : MakeRow();
                if (!row.Go.activeSelf) row.Go.SetActive(true);
                row.Rect.anchoredPosition = _anchor - new Vector2(0f, i * (RowHeight + RowGap));
                if (row.Icon.sprite == null) row.Icon.sprite = _sprite;
                row.Text.text = _lines[i];
            }
            for (int i = _lines.Count; i < _rows.Count; i++) if (_rows[i].Go.activeSelf) _rows[i].Go.SetActive(false);
        }

        void BuildLines()
        {
            long me = Player.m_localPlayer.GetPlayerID();
            // Own ward first (any state, so "defended" / "lost" stay visible), then others' running wards.
            var mine = WardBoard.Entries.Where(e => e.Creator == me).OrderBy(e => WardBoard.IsRunning(e.State) ? 0 : 1).FirstOrDefault();
            if (mine != null) _lines.Add(Line(mine, "Your ward"));
            foreach (var e in WardBoard.Entries.Where(e => e.Creator != me && WardBoard.IsRunning(e.State)).OrderBy(e => e.Id.ID))
                _lines.Add(Line(e, e.Name));

            if (_lines.Count == 0 && Cfg.BuildDeadlineMinutes.Value > 0f && !Progress.Has(Progress.BuiltKey) && !Progress.Has(Progress.DeadlineFailedKey)
                && ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(Progress.WorldStartKey, out var s) && long.TryParse(s, out var start))
                _lines.Add("Build ward in " + Fmt(Cfg.BuildDeadlineMinutes.Value * 60.0 - (ZNet.instance.GetTimeSeconds() - start)));
        }

        static string Line(WardBoard.Entry e, string who)
        {
            string paused = e.Paused ? " (paused)" : "";
            switch (e.State)
            {
                case ChallengeState.Idle:
                case ChallengeState.Countdown: return $"{who}: wave in {Fmt(e.RemainingNow)}{paused}";
                case ChallengeState.Active: return $"{who}: hold {Fmt(e.RemainingNow)}";
                case ChallengeState.Won:
                case ChallengeState.Resting: return $"{who}: defended, next in {Fmt(e.RemainingNow)}{paused}";
                case ChallengeState.Lost: return $"{who}: lost";
                default: return null;
            }
        }

        static string Fmt(double seconds)
        {
            if (seconds < 0) seconds = 0;
            var t = TimeSpan.FromSeconds(seconds);
            return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes:00}:{t.Seconds:00}";
        }
    }

    // A minimap pin on every running ward (any player's, any distance), removed when the challenge ends or the ward goes.
    public static class WardPins
    {
        static readonly Dictionary<ZDOID, Minimap.PinData> _pins = new Dictionary<ZDOID, Minimap.PinData>();
        static float _timer;

        public static int Count => _pins.Count;
        public static IEnumerable<string> Describe() => _pins.Select(kv => $"{kv.Key} {kv.Value.m_name} {kv.Value.m_pos}");

        public static void Tick(float dt)
        {
            _timer -= dt;
            if (_timer > 0f) return;
            _timer = 2f;
            var map = Minimap.instance;
            if (map == null || Player.m_localPlayer == null) { Clear(); return; }

            var running = new HashSet<ZDOID>();
            foreach (var e in WardBoard.Entries)
            {
                if (!WardBoard.IsRunning(e.State)) continue;
                running.Add(e.Id);
                string name = e.Name + "'s ward";
                if (_pins.TryGetValue(e.Id, out var pin) && pin.m_name != name) { map.RemovePin(pin); _pins.Remove(e.Id); pin = null; }
                if (pin == null)
                {
                    pin = map.AddPin(e.Pos, Minimap.PinType.Icon3, name, false, false);
                    var sprite = Plugin.WardPrefab?.GetComponent<Piece>()?.m_icon;
                    if (sprite != null) pin.m_icon = sprite;   // marker is created from m_icon on the next pin update
                    _pins[e.Id] = pin;
                }
                else pin.m_pos = e.Pos;
            }
            foreach (var id in _pins.Keys.Where(id => !running.Contains(id)).ToList()) { map.RemovePin(_pins[id]); _pins.Remove(id); }
        }

        public static void Clear()
        {
            if (Minimap.instance != null) foreach (var p in _pins.Values) Minimap.instance.RemovePin(p);
            _pins.Clear();
        }
    }

    [HarmonyPatch(typeof(Hud), "Awake")]
    static class Hud_Awake { static void Postfix(Hud __instance) => WardHud.Create(__instance); }
}
