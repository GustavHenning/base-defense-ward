using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace DevHarness
{
    // Drives the main menu automatically: creates a test character + world if needed and starts it.
    // Disable by launching with -noautostart.
    [HarmonyPatch(typeof(FejdStartup), "Start")]
    public static class AutoStart
    {
        public const string ProfileName = "devtest";
        public const string WorldName = "DevTest";
        public const string WorldSeed = "devtest";
        private static bool _done;

        static void Postfix(FejdStartup __instance)
        {
            if (_done) return;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-noautostart") >= 0) return;
            _done = true;
            __instance.StartCoroutine(Run(__instance));
        }

        static IEnumerator Run(FejdStartup f)
        {
            yield return new WaitForSeconds(2f);
            HarnessPlugin.Log.LogInfo("AutoStart: begin");
            Terminal.m_cheat = true;

            if (!PlayerProfile.HaveProfile(ProfileName))
            {
                HarnessPlugin.Log.LogInfo("AutoStart: creating character");
                f.OnCharacterNew();
                yield return null;
                var nameField = (TMPro.TMP_InputField)AccessTools.Field(typeof(FejdStartup), "m_csNewCharacterName").GetValue(f);
                nameField.text = ProfileName;
                f.OnNewCharacterDone(true);
                yield return null;
            }

            if (!World.HaveWorld(WorldName))
            {
                HarnessPlugin.Log.LogInfo("AutoStart: creating world");
                var w = new World(WorldName, WorldSeed) { m_fileSource = FileHelpers.FileSource.Local, m_needsDB = false };
                SaveSystem.SetSaveNumber(0u);
                w.SaveWorldFWLData(DateTime.Now);
                SaveSystem.InvalidateCache(SaveDataType.World);
            }

            AccessTools.Method(typeof(FejdStartup), "SetSelectedProfile").Invoke(f, new object[] { ProfileName });
            AccessTools.Method(typeof(FejdStartup), "SelectCharacter").Invoke(f, new object[] { ProfileName, FileHelpers.FileSource.Local });
            AccessTools.Method(typeof(FejdStartup), "UpdateWorldList").Invoke(f, new object[] { true });
            var world = (World)AccessTools.Method(typeof(FejdStartup), "FindWorld").Invoke(f, new object[] { WorldName });
            if (world == null) { HarnessPlugin.Log.LogError("AutoStart: world not found after creation"); yield break; }
            AccessTools.Field(typeof(FejdStartup), "m_world").SetValue(f, world);
            AccessTools.Field(typeof(FejdStartup), "m_instantStart").SetValue(f, true);
            Toggle(f, "m_publicServerToggle", false);
            Toggle(f, "m_openServerToggle", false);
            Toggle(f, "m_crossplayServerToggle", false);
            var pw = (TMPro.TMP_InputField)AccessTools.Field(typeof(FejdStartup), "m_serverPassword").GetValue(f);
            if (pw != null) pw.text = "";

            HarnessPlugin.Log.LogInfo("AutoStart: starting world " + world.m_name);
            f.OnWorldStart();
        }

        static void Toggle(FejdStartup f, string field, bool on)
        {
            var t = (UnityEngine.UI.Toggle)AccessTools.Field(typeof(FejdStartup), field).GetValue(f);
            if (t != null) t.isOn = on;
        }
    }
}
