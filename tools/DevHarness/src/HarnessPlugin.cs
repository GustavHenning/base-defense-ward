using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace DevHarness
{
    // Dev-only plugin. Never ship this: it auto-loads a test character/world and opens a local debug socket.
    [BepInPlugin(GUID, NAME, VERSION)]
    public class HarnessPlugin : BaseUnityPlugin
    {
        public const string GUID = "com.night.devharness";
        public const string NAME = "Dev Harness";
        public const string VERSION = "0.1.0";
        public const string PrefabName = "BaseDefenseWard"; // prefab under test, reported by `state`
        public const int Port = 52380;

        public static ManualLogSource Log;
        private DebugServer _server;

        private void Awake()
        {
            Log = Logger;
            new Harmony(GUID).PatchAll(Assembly.GetExecutingAssembly());
            _server = new DebugServer(Port);
            _server.Start();
            Log.LogInfo($"Debug socket listening on 127.0.0.1:{Port}");
        }

        private void Update() => _server?.Pump();
        private void OnDestroy() => _server?.Stop();
    }

    // Skip the valkyrie intro for the test character.
    [HarmonyPatch(typeof(Game), "Awake")]
    static class Game_Awake
    {
        static void Postfix(Game __instance)
        {
            var prof = __instance.GetPlayerProfile();
            if (prof != null && prof.GetFilename() == AutoStart.ProfileName) prof.m_firstSpawn = false;
        }
    }
}
