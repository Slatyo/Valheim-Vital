using SimpleJson;
using State;
using UnityEngine;

namespace Vital.Data
{
    /// <summary>
    /// Persistent player level and XP data.
    /// Stored via State and persists with world saves as JSON.
    /// </summary>
    public class PlayerLevelData : IPlayerData
    {
        /// <summary>Current level (1-100).</summary>
        public int Level { get; set; } = 1;

        /// <summary>Total accumulated XP.</summary>
        public long TotalXP { get; set; } = 0;

        public void Initialize()
        {
            Level = 1;
            TotalXP = 0;
        }

        public string Serialize()
        {
            var obj = new JsonObject
            {
                ["level"] = Level,
                ["xp"] = TotalXP
            };
            return SimpleJson.SimpleJson.SerializeObject(obj);
        }

        public void Deserialize(string data)
        {
            if (string.IsNullOrEmpty(data)) return;

            try
            {
                var obj = SimpleJson.SimpleJson.DeserializeObject<JsonObject>(data);
                if (obj == null) return;

                if (obj.TryGetValue("level", out var levelVal) && levelVal != null)
                {
                    Level = Mathf.Clamp(System.Convert.ToInt32(levelVal), 1, 100);
                }

                if (obj.TryGetValue("xp", out var xpVal) && xpVal != null)
                {
                    TotalXP = System.Convert.ToInt64(xpVal);
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"Failed to deserialize PlayerLevelData: {ex.Message}");
            }
        }

        public bool Validate()
        {
            return Level >= 1 && Level <= 100 && TotalXP >= 0;
        }
    }
}
