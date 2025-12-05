using System;
using System.Collections.Generic;
using Vital.Data;

namespace Vital.Network
{
    /// <summary>
    /// Handles network synchronization of player data between server and clients.
    /// Uses Valheim's ZRoutedRpc for reliable messaging.
    /// </summary>
    public static class VitalNetwork
    {
        private const string RPC_SYNC_DATA = "Vital_SyncData";
        private const string RPC_REQUEST_SYNC = "Vital_RequestSync";

        private static bool _initialized;

        /// <summary>
        /// Initialize network RPCs.
        /// </summary>
        internal static void Initialize()
        {
            if (_initialized) return;

            // Register RPCs when ZRoutedRpc is available
            if (ZRoutedRpc.instance != null)
            {
                RegisterRPCs();
            }
            else
            {
                // Delay registration until ZNet is ready
                Jotunn.Managers.PrefabManager.OnPrefabsRegistered += OnPrefabsRegistered;
            }
        }

        private static void OnPrefabsRegistered()
        {
            Jotunn.Managers.PrefabManager.OnPrefabsRegistered -= OnPrefabsRegistered;
            RegisterRPCs();
        }

        private static void RegisterRPCs()
        {
            if (_initialized) return;
            if (ZRoutedRpc.instance == null) return;

            // Server -> Client: Sync player data
            ZRoutedRpc.instance.Register<string, string>(RPC_SYNC_DATA, RPC_OnSyncData);

            // Client -> Server: Request sync (on connect)
            ZRoutedRpc.instance.Register(RPC_REQUEST_SYNC, RPC_OnRequestSync);

            _initialized = true;
            Plugin.Log.LogInfo("Network RPCs registered");
        }

        /// <summary>
        /// Cleanup network handlers.
        /// </summary>
        internal static void Cleanup()
        {
            _initialized = false;
        }

        #region Server -> Client

        /// <summary>
        /// Sync a specific module's data to a client.
        /// Server only.
        /// </summary>
        /// <param name="playerId">Target player's ID.</param>
        /// <param name="moduleId">Module to sync.</param>
        public static void SyncToClient(long playerId, string moduleId)
        {
            if (!Plugin.IsServer()) return;
            if (ZRoutedRpc.instance == null) return;
            if (playerId == 0) return;

            string data = VitalDataStore.SerializeModule(playerId, moduleId);
            if (string.IsNullOrEmpty(data)) return;

            try
            {
                ZRoutedRpc.instance.InvokeRoutedRPC(playerId, RPC_SYNC_DATA, moduleId, data);
                Plugin.Log.LogDebug($"Synced {moduleId} to player {playerId}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to sync {moduleId} to player {playerId}: {ex}");
            }
        }

        /// <summary>
        /// Sync all modules to a specific client.
        /// Server only.
        /// </summary>
        /// <param name="playerId">Target player's ID.</param>
        public static void SyncAllToClient(long playerId)
        {
            if (!Plugin.IsServer()) return;

            foreach (string moduleId in VitalDataStore.GetRegisteredModules())
            {
                SyncToClient(playerId, moduleId);
            }
        }

        /// <summary>
        /// Broadcast a module update to all clients.
        /// Server only.
        /// </summary>
        /// <param name="moduleId">Module to broadcast.</param>
        public static void BroadcastModule(string moduleId)
        {
            if (!Plugin.IsServer()) return;
            if (ZNet.instance == null) return;

            foreach (var peer in ZNet.instance.GetConnectedPeers())
            {
                if (peer.m_characterID != null)
                {
                    long playerId = peer.m_characterID.UserID;
                    SyncToClient(playerId, moduleId);
                }
            }
        }

        #endregion

        #region Client -> Server

        /// <summary>
        /// Request sync of all data from server.
        /// Client only.
        /// </summary>
        public static void RequestSync()
        {
            if (Plugin.IsServer()) return;
            if (ZRoutedRpc.instance == null) return;

            try
            {
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), RPC_REQUEST_SYNC);
                Plugin.Log.LogDebug("Requested data sync from server");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to request sync: {ex}");
            }
        }

        #endregion

        #region RPC Handlers

        /// <summary>
        /// Client handler: Receive synced data from server.
        /// </summary>
        private static void RPC_OnSyncData(long sender, string moduleId, string data)
        {
            // Only process if we're a client
            if (Plugin.IsServer()) return;

            Plugin.Log.LogDebug($"Received sync for module {moduleId}");
            VitalDataStore.OnSyncReceived(moduleId, data);
        }

        /// <summary>
        /// Server handler: Client requesting data sync.
        /// </summary>
        private static void RPC_OnRequestSync(long sender)
        {
            // Only process if we're the server
            if (!Plugin.IsServer()) return;

            Plugin.Log.LogDebug($"Player {sender} requested data sync");
            SyncAllToClient(sender);
        }

        #endregion

        #region Connection Handling

        /// <summary>
        /// Called when a player connects. Syncs all their data.
        /// </summary>
        /// <param name="playerId">Connected player's ID.</param>
        internal static void OnPlayerConnected(long playerId)
        {
            if (!Plugin.IsServer()) return;

            Plugin.Log.LogDebug($"Player {playerId} connected, syncing data...");
            SyncAllToClient(playerId);
        }

        /// <summary>
        /// Called when a player disconnects.
        /// </summary>
        /// <param name="playerId">Disconnected player's ID.</param>
        internal static void OnPlayerDisconnected(long playerId)
        {
            // Data persists in memory until world save
            Plugin.Log.LogDebug($"Player {playerId} disconnected");
        }

        #endregion
    }
}
