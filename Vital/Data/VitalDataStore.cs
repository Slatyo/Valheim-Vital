using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SimpleJson;
using Vital.Network;

namespace Vital.Data
{
    /// <summary>
    /// Central player data store for the mod ecosystem.
    /// Provides server-authoritative, extensible data storage per player.
    /// Other mods register their data types here for automatic persistence and sync.
    /// Data is persisted to JSON files in a world-specific folder.
    /// </summary>
    public static class VitalDataStore
    {
        private static readonly Dictionary<string, IDataModule> _modules = new();
        private static readonly object _lock = new();

        private const string FOLDER_NAME = "Vital";
        private const string DATA_FILE = "playerdata.json";
        private const int DATA_VERSION = 1;

        /// <summary>Event fired when any data is changed.</summary>
        public static event Action<long, string> OnDataChanged;

        /// <summary>Event fired when data is synced to client.</summary>
        public static event Action<string> OnDataSynced;

        /// <summary>
        /// Initialize the data store.
        /// </summary>
        internal static void Initialize()
        {
            Plugin.Log.LogInfo("VitalDataStore initialized");
        }

        #region File Storage

        /// <summary>
        /// Get the save path for the current world.
        /// </summary>
        private static string GetSavePath()
        {
            if (ZNet.instance == null)
                return null;

            string worldName = ZNet.instance.GetWorldName();
            if (string.IsNullOrEmpty(worldName))
                return null;

            // Use the world save path like Atlas does
            string worldSavePath = World.GetWorldSavePath(FileHelpers.FileSource.Local);
            return Path.Combine(worldSavePath, FOLDER_NAME, worldName);
        }

        /// <summary>
        /// Ensure the save directory exists.
        /// </summary>
        private static bool EnsureSaveDirectory()
        {
            string savePath = GetSavePath();
            if (string.IsNullOrEmpty(savePath))
                return false;

            try
            {
                if (!Directory.Exists(savePath))
                {
                    Directory.CreateDirectory(savePath);
                    Plugin.Log.LogInfo($"Created save directory: {savePath}");
                }
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to create save directory: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Module Registration

        /// <summary>
        /// Register a player data type. Call this in your mod's Awake().
        /// </summary>
        /// <typeparam name="T">The data type implementing IPlayerData.</typeparam>
        /// <param name="moduleId">Unique identifier for this module (e.g., "viking", "quests").</param>
        /// <example>
        /// <code>
        /// VitalDataStore.Register&lt;VikingPlayerData&gt;("viking");
        /// </code>
        /// </example>
        public static void Register<T>(string moduleId) where T : class, IPlayerData, new()
        {
            if (string.IsNullOrWhiteSpace(moduleId))
            {
                Plugin.Log.LogError("Cannot register module with empty ID");
                return;
            }

            lock (_lock)
            {
                if (_modules.ContainsKey(moduleId))
                {
                    Plugin.Log.LogWarning($"Data module '{moduleId}' already registered, skipping");
                    return;
                }

                _modules[moduleId] = new DataModule<T>(moduleId);
                Plugin.Log.LogInfo($"Registered data module: {moduleId}");
            }
        }

        /// <summary>
        /// Unregister a data module. Call in OnDestroy if needed.
        /// </summary>
        /// <param name="moduleId">Module identifier.</param>
        public static void Unregister(string moduleId)
        {
            lock (_lock)
            {
                if (_modules.Remove(moduleId))
                {
                    Plugin.Log.LogInfo($"Unregistered data module: {moduleId}");
                }
            }
        }

        /// <summary>
        /// Check if a module is registered.
        /// </summary>
        /// <param name="moduleId">Module identifier.</param>
        /// <returns>True if registered.</returns>
        public static bool IsRegistered(string moduleId)
        {
            lock (_lock)
            {
                return _modules.ContainsKey(moduleId);
            }
        }

        /// <summary>
        /// Get all registered module IDs.
        /// </summary>
        /// <returns>Collection of module IDs.</returns>
        public static IEnumerable<string> GetRegisteredModules()
        {
            lock (_lock)
            {
                return new List<string>(_modules.Keys);
            }
        }

        #endregion

        #region Data Access

        /// <summary>
        /// Get player data for a specific module.
        /// Creates default data if none exists.
        /// </summary>
        /// <typeparam name="T">The data type.</typeparam>
        /// <param name="player">The player.</param>
        /// <param name="moduleId">Module identifier.</param>
        /// <returns>The player's data, or null if module not registered.</returns>
        public static T Get<T>(Player player, string moduleId) where T : class, IPlayerData, new()
        {
            if (player == null) return null;
            return Get<T>(player.GetPlayerID(), moduleId);
        }

        /// <summary>
        /// Get player data for a specific module by player ID.
        /// Creates default data if none exists.
        /// </summary>
        /// <typeparam name="T">The data type.</typeparam>
        /// <param name="playerId">Player's unique ID.</param>
        /// <param name="moduleId">Module identifier.</param>
        /// <returns>The player's data, or null if module not registered.</returns>
        public static T Get<T>(long playerId, string moduleId) where T : class, IPlayerData, new()
        {
            lock (_lock)
            {
                if (!_modules.TryGetValue(moduleId, out var module))
                {
                    Plugin.Log.LogError($"Data module '{moduleId}' not registered");
                    return null;
                }

                if (module is DataModule<T> typedModule)
                {
                    return typedModule.GetData(playerId);
                }

                Plugin.Log.LogError($"Data module '{moduleId}' type mismatch. Expected {typeof(T).Name}");
                return null;
            }
        }

        /// <summary>
        /// Set player data. Server only - triggers sync to client.
        /// </summary>
        /// <typeparam name="T">The data type.</typeparam>
        /// <param name="player">The player.</param>
        /// <param name="moduleId">Module identifier.</param>
        /// <param name="data">Data to set.</param>
        public static void Set<T>(Player player, string moduleId, T data) where T : class, IPlayerData, new()
        {
            if (player == null) return;
            Set(player.GetPlayerID(), moduleId, data);
        }

        /// <summary>
        /// Set player data by player ID. Server only - triggers sync to client.
        /// </summary>
        /// <typeparam name="T">The data type.</typeparam>
        /// <param name="playerId">Player's unique ID.</param>
        /// <param name="moduleId">Module identifier.</param>
        /// <param name="data">Data to set.</param>
        public static void Set<T>(long playerId, string moduleId, T data) where T : class, IPlayerData, new()
        {
            if (!Plugin.IsServer())
            {
                Plugin.Log.LogError("VitalDataStore.Set() can only be called on server");
                return;
            }

            lock (_lock)
            {
                if (!_modules.TryGetValue(moduleId, out var module))
                {
                    Plugin.Log.LogError($"Data module '{moduleId}' not registered");
                    return;
                }

                if (module is DataModule<T> typedModule)
                {
                    typedModule.SetData(playerId, data);
                    OnDataChanged?.Invoke(playerId, moduleId);
                    VitalNetwork.SyncToClient(playerId, moduleId);
                }
                else
                {
                    Plugin.Log.LogError($"Data module '{moduleId}' type mismatch");
                }
            }
        }

        /// <summary>
        /// Mark data as dirty (needs sync). Call after modifying data directly.
        /// Server only.
        /// </summary>
        /// <param name="playerId">Player's unique ID.</param>
        /// <param name="moduleId">Module identifier.</param>
        public static void MarkDirty(long playerId, string moduleId)
        {
            if (!Plugin.IsServer()) return;

            try
            {
                OnDataChanged?.Invoke(playerId, moduleId);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in OnDataChanged handler: {ex}");
            }

            VitalNetwork.SyncToClient(playerId, moduleId);
        }

        /// <summary>
        /// Mark data as dirty for a player. Server only.
        /// </summary>
        /// <param name="player">The player.</param>
        /// <param name="moduleId">Module identifier.</param>
        public static void MarkDirty(Player player, string moduleId)
        {
            if (player != null)
            {
                MarkDirty(player.GetPlayerID(), moduleId);
            }
        }

        #endregion

        #region Serialization

        /// <summary>
        /// Serialize a single module's data for a player (for network sync).
        /// </summary>
        internal static string SerializeModule(long playerId, string moduleId)
        {
            lock (_lock)
            {
                if (_modules.TryGetValue(moduleId, out var module))
                {
                    return module.Serialize(playerId);
                }
                return null;
            }
        }

        /// <summary>
        /// Deserialize a single module's data for a player (from network sync).
        /// </summary>
        internal static void DeserializeModule(long playerId, string moduleId, string data)
        {
            lock (_lock)
            {
                if (_modules.TryGetValue(moduleId, out var module))
                {
                    module.Deserialize(playerId, data);
                }
            }
        }

        /// <summary>
        /// Save all player data to JSON file in world-specific folder.
        /// Called on world save.
        /// Structure: { "version": 1, "modules": { "moduleId": { "playerId": {...}, ... }, ... } }
        /// </summary>
        internal static void SaveToWorld()
        {
            if (!Plugin.IsServer())
            {
                Plugin.Log.LogDebug("SaveToWorld: Not server, skipping");
                return;
            }

            if (!EnsureSaveDirectory())
            {
                Plugin.Log.LogError("SaveToWorld: Failed to ensure save directory exists");
                return;
            }

            string savePath = GetSavePath();
            if (string.IsNullOrEmpty(savePath))
            {
                Plugin.Log.LogDebug("SaveToWorld: No valid save path");
                return;
            }

            string filePath = Path.Combine(savePath, DATA_FILE);

            try
            {
                var root = new JsonObject
                {
                    ["version"] = DATA_VERSION,
                    ["savedAt"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                var modulesData = new JsonObject();

                lock (_lock)
                {
                    foreach (var kvp in _modules)
                    {
                        string moduleId = kvp.Key;
                        var module = kvp.Value;
                        var moduleData = new JsonObject();

                        var playerIds = module.GetAllPlayerIds().ToList();

                        foreach (long playerId in playerIds)
                        {
                            string serialized = module.Serialize(playerId);
                            if (!string.IsNullOrEmpty(serialized))
                            {
                                // Store the raw JSON data, not double-encoded
                                moduleData[playerId.ToString()] = SimpleJson.SimpleJson.DeserializeObject(serialized);
                            }
                        }

                        if (moduleData.Count > 0)
                        {
                            modulesData[moduleId] = moduleData;
                        }
                    }
                }

                root["modules"] = modulesData;

                string json = SimpleJson.SimpleJson.SerializeObject(root);

                // Write with backup
                string backupPath = filePath + ".bak";
                if (File.Exists(filePath))
                {
                    File.Copy(filePath, backupPath, true);
                }

                File.WriteAllText(filePath, json, Encoding.UTF8);

                Plugin.Log.LogInfo($"Saved player data to {filePath}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to save player data: {ex}");
            }
        }

        /// <summary>
        /// Load all player data from JSON file in world-specific folder.
        /// Called on world load.
        /// </summary>
        internal static void LoadFromWorld()
        {
            if (!Plugin.IsServer())
            {
                return;
            }

            string savePath = GetSavePath();
            if (string.IsNullOrEmpty(savePath))
            {
                return;
            }

            string filePath = Path.Combine(savePath, DATA_FILE);
            if (!File.Exists(filePath))
            {
                Plugin.Log.LogDebug("No saved player data file found");
                return;
            }

            try
            {
                string json = File.ReadAllText(filePath, Encoding.UTF8);

                if (string.IsNullOrEmpty(json))
                {
                    Plugin.Log.LogDebug("Empty player data file");
                    return;
                }

                var root = SimpleJson.SimpleJson.DeserializeObject<JsonObject>(json);
                if (root == null)
                {
                    Plugin.Log.LogWarning("Failed to parse player data JSON");
                    return;
                }

                // Check version
                if (root.TryGetValue("version", out var versionObj))
                {
                    int version = Convert.ToInt32(versionObj);
                    if (version > DATA_VERSION)
                    {
                        Plugin.Log.LogWarning($"Player data version {version} is newer than supported {DATA_VERSION}");
                    }
                }

                if (!root.TryGetValue("modules", out var modulesObj) || !(modulesObj is JsonObject modulesData))
                {
                    Plugin.Log.LogDebug("No modules data in save file");
                    return;
                }

                int loadedModules = 0;
                int loadedPlayers = 0;

                lock (_lock)
                {
                    foreach (var moduleKvp in modulesData)
                    {
                        string moduleId = moduleKvp.Key;

                        if (!_modules.TryGetValue(moduleId, out var module))
                        {
                            Plugin.Log.LogWarning($"Module '{moduleId}' not registered, skipping saved data");
                            continue;
                        }

                        if (moduleKvp.Value is JsonObject moduleData)
                        {
                            foreach (var playerKvp in moduleData)
                            {
                                if (!long.TryParse(playerKvp.Key, out long playerId))
                                    continue;

                                // Re-serialize the nested object to pass to module deserializer
                                string serialized = SimpleJson.SimpleJson.SerializeObject(playerKvp.Value);
                                if (!string.IsNullOrEmpty(serialized))
                                {
                                    module.Deserialize(playerId, serialized);
                                    loadedPlayers++;
                                }
                            }
                            loadedModules++;
                        }
                    }
                }

                Plugin.Log.LogInfo($"Loaded player data: {loadedModules} modules, {loadedPlayers} player entries from {filePath}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to load player data: {ex}");
            }
        }

        /// <summary>
        /// Clear all data (called on world unload).
        /// </summary>
        internal static void ClearAll()
        {
            lock (_lock)
            {
                foreach (var module in _modules.Values)
                {
                    module.Clear();
                }
            }
            Plugin.Log.LogDebug("Cleared all player data");
        }

        #endregion

        #region Client Sync

        /// <summary>
        /// Called on client when data is received from server.
        /// </summary>
        internal static void OnSyncReceived(string moduleId, string data)
        {
            long localPlayerId = Player.m_localPlayer?.GetPlayerID() ?? 0;
            if (localPlayerId == 0) return;

            DeserializeModule(localPlayerId, moduleId, data);

            try
            {
                OnDataSynced?.Invoke(moduleId);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in OnDataSynced handler: {ex}");
            }

            Plugin.Log.LogDebug($"Synced {moduleId} data from server");
        }

        /// <summary>
        /// Sync all data to a specific client (on connect).
        /// </summary>
        internal static void SyncAllToClient(long playerId)
        {
            if (!Plugin.IsServer()) return;

            lock (_lock)
            {
                foreach (var moduleId in _modules.Keys)
                {
                    VitalNetwork.SyncToClient(playerId, moduleId);
                }
            }
        }

        #endregion
    }
}
