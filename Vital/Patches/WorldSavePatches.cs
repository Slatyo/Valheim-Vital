using System;
using HarmonyLib;
using Vital.Data;
using Vital.Network;

namespace Vital.Patches
{
    /// <summary>
    /// Harmony patches for world save/load to persist player data.
    /// Data is stored as JSON in a world-specific folder for proper persistence.
    /// </summary>
    [HarmonyPatch]
    internal static class WorldSavePatches
    {
        /// <summary>
        /// Save player data when world is saved.
        /// Saves to Vital/worldname/playerdata.json in the world save folder.
        /// </summary>
        [HarmonyPatch(typeof(ZNet), nameof(ZNet.SaveWorld))]
        [HarmonyPrefix]
        private static void ZNet_SaveWorld_Prefix(ZNet __instance, bool sync)
        {
            if (!Plugin.IsServer()) return;

            // Save data to JSON file before the world saves
            VitalDataStore.SaveToWorld();
        }

        /// <summary>
        /// Load player data when ZNet starts (world loaded).
        /// </summary>
        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Start))]
        [HarmonyPostfix]
        private static void ZNet_Start_Postfix(ZNet __instance)
        {
            if (!Plugin.IsServer()) return;

            // Load data from JSON file after world is loaded
            VitalDataStore.LoadFromWorld();
        }

        /// <summary>
        /// Save and clear data when leaving world.
        /// Prefix: Save first, then let shutdown proceed.
        /// </summary>
        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Shutdown))]
        [HarmonyPrefix]
        private static void ZNet_Shutdown_Prefix()
        {
            // Save data before shutdown clears everything
            if (Plugin.IsServer())
            {
                Plugin.Log.LogInfo("ZNet.Shutdown - saving player data before shutdown");
                VitalDataStore.SaveToWorld();
            }
        }

        /// <summary>
        /// Clear data after shutdown is complete.
        /// </summary>
        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Shutdown))]
        [HarmonyPostfix]
        private static void ZNet_Shutdown_Postfix()
        {
            VitalDataStore.ClearAll();
        }
    }

    /// <summary>
    /// Patches for player connection handling.
    /// </summary>
    [HarmonyPatch]
    internal static class PlayerConnectionPatches
    {
        /// <summary>
        /// Sync data when player spawns.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        [HarmonyPostfix]
        private static void Player_OnSpawned_Postfix(Player __instance)
        {
            if (__instance == null) return;

            // Server: sync data to this player
            if (Plugin.IsServer())
            {
                long playerId = __instance.GetPlayerID();
                if (playerId != 0)
                {
                    VitalNetwork.OnPlayerConnected(playerId);
                }
            }
            // Client: request sync from server
            else if (__instance == Player.m_localPlayer)
            {
                VitalNetwork.RequestSync();
            }
        }

        /// <summary>
        /// Handle player disconnect.
        /// </summary>
        [HarmonyPatch(typeof(ZNet), nameof(ZNet.RPC_Disconnect))]
        [HarmonyPrefix]
        private static void ZNet_RPC_Disconnect_Prefix(ZNet __instance, ZRpc rpc)
        {
            if (!Plugin.IsServer()) return;

            var peer = __instance.GetPeer(rpc);
            if (peer?.m_characterID != null)
            {
                VitalNetwork.OnPlayerDisconnected(peer.m_characterID.UserID);
            }
        }
    }
}
