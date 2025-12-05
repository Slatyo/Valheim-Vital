using System;
using System.Collections.Generic;

namespace Vital.Data
{
    /// <summary>
    /// Interface for type-erased data module operations.
    /// </summary>
    internal interface IDataModule
    {
        /// <summary>Module identifier.</summary>
        string ModuleId { get; }

        /// <summary>Serialize a player's data.</summary>
        string Serialize(long playerId);

        /// <summary>Deserialize data for a player.</summary>
        void Deserialize(long playerId, string data);

        /// <summary>Check if data exists for player.</summary>
        bool HasData(long playerId);

        /// <summary>Remove data for player.</summary>
        void RemoveData(long playerId);

        /// <summary>Get all player IDs with data.</summary>
        IEnumerable<long> GetAllPlayerIds();

        /// <summary>Clear all data.</summary>
        void Clear();
    }

    /// <summary>
    /// Generic data module that stores player data of a specific type.
    /// </summary>
    /// <typeparam name="T">The player data type implementing IPlayerData.</typeparam>
    internal class DataModule<T> : IDataModule where T : class, IPlayerData, new()
    {
        private readonly string _moduleId;
        private readonly Dictionary<long, T> _playerData = new();
        private readonly object _lock = new();

        /// <summary>Module identifier.</summary>
        public string ModuleId => _moduleId;

        /// <summary>
        /// Create a new data module.
        /// </summary>
        /// <param name="moduleId">Unique identifier for this module.</param>
        public DataModule(string moduleId)
        {
            _moduleId = moduleId;
        }

        /// <summary>
        /// Get data for a player, creating default if not exists.
        /// </summary>
        /// <param name="playerId">Player's unique ID.</param>
        /// <returns>Player's data instance.</returns>
        public T GetData(long playerId)
        {
            lock (_lock)
            {
                if (!_playerData.TryGetValue(playerId, out var data))
                {
                    data = new T();
                    data.Initialize();
                    _playerData[playerId] = data;
                }
                return data;
            }
        }

        /// <summary>
        /// Set data for a player.
        /// </summary>
        /// <param name="playerId">Player's unique ID.</param>
        /// <param name="data">Data to set.</param>
        public void SetData(long playerId, T data)
        {
            lock (_lock)
            {
                _playerData[playerId] = data;
            }
        }

        /// <summary>
        /// Serialize player data to string.
        /// </summary>
        public string Serialize(long playerId)
        {
            lock (_lock)
            {
                if (_playerData.TryGetValue(playerId, out var data))
                {
                    try
                    {
                        return data.Serialize();
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogError($"Failed to serialize {_moduleId} data for player {playerId}: {ex}");
                        return null;
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// Deserialize and store player data.
        /// </summary>
        public void Deserialize(long playerId, string data)
        {
            if (string.IsNullOrEmpty(data)) return;

            lock (_lock)
            {
                try
                {
                    var playerData = new T();
                    playerData.Deserialize(data);

                    if (!playerData.Validate())
                    {
                        Plugin.Log.LogWarning($"Invalid {_moduleId} data for player {playerId}, reinitializing");
                        playerData = new T();
                        playerData.Initialize();
                    }

                    _playerData[playerId] = playerData;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"Failed to deserialize {_moduleId} data for player {playerId}: {ex}");
                    // Create fresh data on error
                    var freshData = new T();
                    freshData.Initialize();
                    _playerData[playerId] = freshData;
                }
            }
        }

        /// <summary>
        /// Check if player has data.
        /// </summary>
        public bool HasData(long playerId)
        {
            lock (_lock)
            {
                return _playerData.ContainsKey(playerId);
            }
        }

        /// <summary>
        /// Remove player's data.
        /// </summary>
        public void RemoveData(long playerId)
        {
            lock (_lock)
            {
                _playerData.Remove(playerId);
            }
        }

        /// <summary>
        /// Get all player IDs with data.
        /// </summary>
        public IEnumerable<long> GetAllPlayerIds()
        {
            lock (_lock)
            {
                return new List<long>(_playerData.Keys);
            }
        }

        /// <summary>
        /// Clear all data.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _playerData.Clear();
            }
        }
    }
}
