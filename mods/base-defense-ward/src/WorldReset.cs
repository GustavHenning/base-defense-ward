using System.Collections;
using UnityEngine;

namespace BaseDefenseWard
{
    // "Server is deleted / restarts if the challenge is lost." Server-side only.
    public static class WorldReset
    {
        public static bool Pending { get; private set; }
        public const float DelaySeconds = 10f;

        public static void Schedule(string reason)
        {
            if (Pending || ZNet.instance == null || !ZNet.instance.IsServer()) return;
            Pending = true;
            Plugin.Log.LogWarning($"World reset scheduled in {DelaySeconds}s: {reason}");
            WardChallenge.Say($"This world will be deleted in {DelaySeconds:0} seconds.");
            Plugin.Instance.StartCoroutine(Run());
        }

        static IEnumerator Run()
        {
            yield return new WaitForSeconds(DelaySeconds);
            var world = ZNet.World;
            string name = world?.m_name; var source = world?.m_fileSource ?? FileHelpers.FileSource.Local;
            bool dedicated = ZNet.instance.IsDedicated();
            Plugin.Log.LogWarning($"Deleting world '{name}' ({source}) and shutting down");
            // Game.Shutdown(false) = the no-save half of Logout (Logout itself re-enables saving after a disk-space check).
            HarmonyLib.AccessTools.Method(typeof(Game), "Shutdown").Invoke(Game.instance, new object[] { false });
            if (!string.IsNullOrEmpty(name)) World.RemoveWorld(name, source);
            SaveSystem.InvalidateCache(SaveDataType.World);
            yield return null;
            if (dedicated) Application.Quit();
            else SystemResourceManager.FastLoadScene(Game.instance.m_startScene);
        }
    }
}
