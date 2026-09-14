using System;
using System.Collections.Generic;
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
        public const string VERSION = "0.1.0";

        public const string PrefabName = "BaseDefenseWard";
        public const string SourcePrefab = "guard_stone";

        public static readonly Color GlowColor = new Color(0.2f, 0.5f, 1f);
        public static ManualLogSource Log;
        public static GameObject WardPrefab;
        private static GameObject _holder;

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo($"{NAME} {VERSION} loading");
            _holder = new GameObject("BaseDefenseWard_PrefabHolder");
            _holder.SetActive(false);
            DontDestroyOnLoad(_holder);
            new Harmony(GUID).PatchAll(Assembly.GetExecutingAssembly());
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
                piece.m_description = "A ward that glows blue while active.";
                piece.m_resources = Array.Empty<Piece.Requirement>(); // free, for testing
                var area = WardPrefab.GetComponent<PrivateArea>();
                if (area != null)
                {
                    area.m_name = "Base Defense Ward";
                    // Blue glow: a point light that lives under the enabled-effect object, so it only
                    // shows while the ward is activated (PrivateArea toggles that object on/off).
                    if (area.m_enabledEffect != null)
                    {
                        foreach (var l in area.m_enabledEffect.GetComponentsInChildren<Light>(true)) { l.color = GlowColor; l.intensity *= 1.5f; }
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
                        light.intensity = 3f;
                        light.range = 8f;
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

    // Tint the ward's emission blue on our clone whenever its status refreshes (vanilla ward keeps its own colour
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
