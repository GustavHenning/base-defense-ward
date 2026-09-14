using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace BaseDefenseWard
{
    // One ward per player: building a new one removes that player's previous ward (so the ward can be moved by
    // rebuilding it), and building is refused while the player's current ward has a wave in progress.
    public static class OnePerPlayer
    {
        public static readonly int HState = "bdw_state".GetStableHashCode();
        public static int BlockedCount, RemovedCount; // session counters, surfaced by DebugCommand("wards")

        public static bool IsWard(Piece piece) => piece != null && piece.gameObject.name.StartsWith(Plugin.PrefabName);

        // Every ward ZDO (loaded or not) created by this player id.
        public static List<ZDO> WardsOf(long playerId)
        {
            var all = new List<ZDO>(); int index = 0;
            while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(Plugin.PrefabName, all, ref index)) { }
            return all.FindAll(z => z.GetLong(ZDOVars.s_creator) == playerId);
        }

        public static bool HasWaveInProgress(long playerId) =>
            WardsOf(playerId).Exists(z => z.GetInt(HState) == (int)ChallengeState.Active);

        public static int RemoveOtherWards(long playerId, ZDOID keep)
        {
            int n = 0;
            foreach (var z in WardsOf(playerId))
            {
                if (z.m_uid == keep) continue;
                var go = ZNetScene.instance.FindInstance(z.m_uid);
                if (go != null) { var nv = go.GetComponent<ZNetView>(); nv.ClaimOwnership(); nv.Destroy(); }
                else { z.SetOwner(ZDOMan.GetSessionID()); ZDOMan.instance.DestroyZDO(z); }
                n++;
            }
            RemovedCount += n;
            return n;
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
    static class Player_TryPlacePiece
    {
        static bool Prefix(Player __instance, Piece piece, ref bool __result)
        {
            if (!OnePerPlayer.IsWard(piece) || !OnePerPlayer.HasWaveInProgress(__instance.GetPlayerID())) return true;
            __instance.Message(MessageHud.MessageType.Center, "A wave is attacking your ward. You cannot move it now.");
            OnePerPlayer.BlockedCount++;
            Plugin.Log.LogInfo("Ward placement blocked: wave in progress");
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
    static class Player_PlacePiece
    {
        static void Postfix(Player __instance, Piece piece, Vector3 pos)
        {
            if (!OnePerPlayer.IsWard(piece)) return;
            // The just-placed ward is the one at `pos`; everything else this player built goes away.
            ZDOID keep = ZDOID.None;
            foreach (var z in OnePerPlayer.WardsOf(__instance.GetPlayerID()))
                if (Vector3.Distance(z.GetPosition(), pos) < 0.5f) keep = z.m_uid;
            int removed = OnePerPlayer.RemoveOtherWards(__instance.GetPlayerID(), keep);
            if (removed > 0)
            {
                __instance.Message(MessageHud.MessageType.TopLeft, "Previous ward removed. The countdown restarts here.");
                Plugin.Log.LogInfo($"Removed {removed} previous ward(s) for player {__instance.GetPlayerID()}");
            }
        }
    }
}
