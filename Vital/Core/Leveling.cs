using System;
using UnityEngine;
using Vital.Data;

namespace Vital.Core
{
    /// <summary>
    /// Core leveling system providing level storage, XP formulas, and kill XP calculations.
    /// Used by both Viking (players) and Denizen (creatures).
    /// </summary>
    public static class Leveling
    {
        /// <summary>Maximum achievable level.</summary>
        public const int MaxLevel = 100;

        /// <summary>Minimum level.</summary>
        public const int MinLevel = 1;

        /// <summary>ZDO key for storing creature level (non-players).</summary>
        private const string ZDO_LEVEL_KEY = "vital_level";

        /// <summary>Module ID for player level data in VitalDataStore.</summary>
        public const string MODULE_ID = "vital_level";

        /// <summary>Event fired when any entity's level changes.</summary>
        public static event Action<Character, int, int> OnLevelChanged;

        /// <summary>
        /// Initialize the leveling system.
        /// </summary>
        internal static void Initialize()
        {
            // Register player level data module
            VitalDataStore.Register<PlayerLevelData>(MODULE_ID);
            Plugin.Log.LogInfo("Leveling system initialized");
        }

        #region Player Level Storage (Persistent via VitalDataStore)

        /// <summary>
        /// Get level data for a player.
        /// </summary>
        private static PlayerLevelData GetPlayerData(Player player)
        {
            if (player == null) return null;
            return VitalDataStore.Get<PlayerLevelData>(player, MODULE_ID);
        }

        /// <summary>
        /// Get the level of a player.
        /// </summary>
        /// <param name="player">The player to get level for.</param>
        /// <returns>The player's level (1-100).</returns>
        public static int GetLevel(Player player)
        {
            var data = GetPlayerData(player);
            return data?.Level ?? MinLevel;
        }

        /// <summary>
        /// Set the level of a player.
        /// </summary>
        /// <param name="player">The player to set level for.</param>
        /// <param name="level">The new level (clamped to 1-100).</param>
        public static void SetLevel(Player player, int level)
        {
            var data = GetPlayerData(player);
            if (data == null) return;

            int oldLevel = data.Level;
            int newLevel = Mathf.Clamp(level, MinLevel, MaxLevel);

            if (oldLevel == newLevel) return;

            data.Level = newLevel;
            data.TotalXP = GetCumulativeXP(newLevel); // Sync XP to match level
            VitalDataStore.MarkDirty(player, MODULE_ID);

            // Fire event
            try
            {
                OnLevelChanged?.Invoke(player, oldLevel, newLevel);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in OnLevelChanged handler: {ex}");
            }

            Plugin.Log.LogDebug($"Set level for player: {oldLevel} -> {newLevel}");
        }

        /// <summary>
        /// Get the total accumulated XP for a player.
        /// </summary>
        /// <param name="player">The player to get XP for.</param>
        /// <returns>Total XP accumulated.</returns>
        public static long GetXP(Player player)
        {
            var data = GetPlayerData(player);
            return data?.TotalXP ?? 0;
        }

        /// <summary>
        /// Set the total XP for a player.
        /// </summary>
        /// <param name="player">The player to set XP for.</param>
        /// <param name="xp">Total XP value.</param>
        public static void SetXP(Player player, long xp)
        {
            var data = GetPlayerData(player);
            if (data == null) return;

            data.TotalXP = Math.Max(0, xp);
            data.Level = GetLevelForXP(data.TotalXP);
            VitalDataStore.MarkDirty(player, MODULE_ID);
        }

        /// <summary>
        /// Add XP to a player and update their level if necessary.
        /// </summary>
        /// <param name="player">The player to add XP to.</param>
        /// <param name="amount">Amount of XP to add.</param>
        public static void AddXP(Player player, long amount)
        {
            if (player == null || amount <= 0) return;

            var data = GetPlayerData(player);
            if (data == null) return;

            int oldLevel = data.Level;
            data.TotalXP += amount;
            data.Level = GetLevelForXP(data.TotalXP);
            VitalDataStore.MarkDirty(player, MODULE_ID);

            if (data.Level != oldLevel)
            {
                try
                {
                    OnLevelChanged?.Invoke(player, oldLevel, data.Level);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"Error in OnLevelChanged handler: {ex}");
                }
            }

            Plugin.Log.LogDebug($"Added {amount} XP to player. Total: {data.TotalXP}, Level: {data.Level}");
        }

        #endregion

        #region Creature Level Storage (ZDO-based, not persistent across sessions)

        /// <summary>
        /// Get the ZDO for a character safely.
        /// </summary>
        private static ZDO GetZDO(Character entity)
        {
            if (entity == null) return null;
            var nview = entity.GetComponent<ZNetView>();
            return nview?.GetZDO();
        }

        /// <summary>
        /// Get the level of any Character entity (creature, NPC - not players).
        /// For players, use the Player overload.
        /// </summary>
        /// <param name="entity">The character to get level for.</param>
        /// <returns>The entity's level (1-100), or 1 if not set.</returns>
        public static int GetLevel(Character entity)
        {
            // If it's a player, use the persistent storage
            if (entity is Player player)
            {
                return GetLevel(player);
            }

            // For creatures/NPCs, use ZDO
            var zdo = GetZDO(entity);
            if (zdo == null) return MinLevel;

            return Mathf.Clamp(zdo.GetInt(ZDO_LEVEL_KEY, MinLevel), MinLevel, MaxLevel);
        }

        /// <summary>
        /// Set the level of a creature/NPC (not player).
        /// For players, use the Player overload.
        /// </summary>
        /// <param name="entity">The character to set level for.</param>
        /// <param name="level">The new level (clamped to 1-100).</param>
        public static void SetLevel(Character entity, int level)
        {
            // If it's a player, use the persistent storage
            if (entity is Player player)
            {
                SetLevel(player, level);
                return;
            }

            // For creatures/NPCs, use ZDO
            var zdo = GetZDO(entity);
            if (zdo == null) return;

            int oldLevel = GetLevel(entity);
            int newLevel = Mathf.Clamp(level, MinLevel, MaxLevel);

            if (oldLevel == newLevel) return;

            zdo.Set(ZDO_LEVEL_KEY, newLevel);

            try
            {
                OnLevelChanged?.Invoke(entity, oldLevel, newLevel);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in OnLevelChanged handler: {ex}");
            }

            Plugin.Log.LogDebug($"Set level for {entity.m_name}: {oldLevel} -> {newLevel}");
        }

        #endregion

        #region XP Formula

        /// <summary>
        /// Calculate XP required to reach a specific level from the previous level.
        /// Uses an exponential curve with steep scaling at high levels.
        /// </summary>
        /// <param name="level">Target level (2-100).</param>
        /// <returns>XP required to go from (level-1) to level.</returns>
        public static long GetXPForLevel(int level)
        {
            if (level <= MinLevel) return 0;
            if (level > MaxLevel) level = MaxLevel;

            // Base exponential growth: level^3.5 * 0.5
            double baseXP = Math.Pow(level, 3.5) * 0.5;

            // Steep increase for prestige levels (90+)
            if (level > 90)
            {
                double highLevelMultiplier = 1.0 + ((level - 90) * 0.1);
                baseXP *= highLevelMultiplier;
            }

            return (long)baseXP;
        }

        /// <summary>
        /// Calculate total XP required from level 1 to reach target level.
        /// </summary>
        /// <param name="level">Target level.</param>
        /// <returns>Cumulative XP required.</returns>
        public static long GetCumulativeXP(int level)
        {
            if (level <= MinLevel) return 0;
            if (level > MaxLevel) level = MaxLevel;

            long total = 0;
            for (int i = 2; i <= level; i++)
            {
                total += GetXPForLevel(i);
            }
            return total;
        }

        /// <summary>
        /// Calculate XP required to go from one level to another.
        /// </summary>
        /// <param name="fromLevel">Starting level.</param>
        /// <param name="toLevel">Target level.</param>
        /// <returns>XP required for the transition.</returns>
        public static long GetXPBetweenLevels(int fromLevel, int toLevel)
        {
            if (fromLevel >= toLevel) return 0;
            return GetCumulativeXP(toLevel) - GetCumulativeXP(fromLevel);
        }

        /// <summary>
        /// Calculate level from total accumulated XP.
        /// </summary>
        /// <param name="totalXP">Total XP accumulated.</param>
        /// <returns>Current level based on XP.</returns>
        public static int GetLevelForXP(long totalXP)
        {
            if (totalXP <= 0) return MinLevel;

            int level = MinLevel;
            long cumulative = 0;

            while (level < MaxLevel)
            {
                long needed = GetXPForLevel(level + 1);
                if (cumulative + needed > totalXP) break;
                cumulative += needed;
                level++;
            }

            return level;
        }

        /// <summary>
        /// Get progress towards next level as a percentage (0.0 - 1.0).
        /// </summary>
        /// <param name="totalXP">Total XP accumulated.</param>
        /// <returns>Progress percentage (0.0 to 1.0).</returns>
        public static float GetLevelProgress(long totalXP)
        {
            int currentLevel = GetLevelForXP(totalXP);
            if (currentLevel >= MaxLevel) return 1.0f;

            long currentLevelXP = GetCumulativeXP(currentLevel);
            long nextLevelXP = GetCumulativeXP(currentLevel + 1);
            long xpInCurrentLevel = totalXP - currentLevelXP;
            long xpNeededForNext = nextLevelXP - currentLevelXP;

            if (xpNeededForNext <= 0) return 1.0f;

            return Mathf.Clamp01((float)xpInCurrentLevel / xpNeededForNext);
        }

        /// <summary>
        /// Get progress towards next level for a player.
        /// </summary>
        /// <param name="player">The player.</param>
        /// <returns>Progress percentage (0.0 to 1.0).</returns>
        public static float GetLevelProgress(Player player)
        {
            return GetLevelProgress(GetXP(player));
        }

        #endregion

        #region Kill XP Calculation

        /// <summary>
        /// Calculate base XP reward for killing a creature of given level.
        /// </summary>
        /// <param name="creatureLevel">The killed creature's level.</param>
        /// <param name="isElite">Whether the creature is elite.</param>
        /// <param name="isBoss">Whether the creature is a boss.</param>
        /// <returns>Base XP reward.</returns>
        public static long GetKillXP(int creatureLevel, bool isElite = false, bool isBoss = false)
        {
            // Base XP scales linearly with creature level
            long baseXP = creatureLevel * 10L;

            // Elite creatures give 3x XP
            if (isElite) baseXP *= 3;

            // Bosses give 10x XP
            if (isBoss) baseXP *= 10;

            return baseXP;
        }

        /// <summary>
        /// Calculate XP reward with level difference modifier.
        /// Lower level creatures give reduced XP, higher level give bonus.
        /// </summary>
        /// <param name="playerLevel">The player's level.</param>
        /// <param name="creatureLevel">The killed creature's level.</param>
        /// <param name="isElite">Whether the creature is elite.</param>
        /// <param name="isBoss">Whether the creature is a boss.</param>
        /// <returns>Modified XP reward.</returns>
        public static long GetKillXP(int playerLevel, int creatureLevel, bool isElite = false, bool isBoss = false)
        {
            long baseXP = GetKillXP(creatureLevel, isElite, isBoss);
            float multiplier = GetLevelDifferenceMultiplier(playerLevel, creatureLevel);
            return (long)(baseXP * multiplier);
        }

        /// <summary>
        /// Get XP multiplier based on level difference between player and target.
        /// Color coding by difficulty:
        /// - Grey (-10+): 10% XP (trivial)
        /// - Green (-5 to -9): 50% XP (easy)
        /// - Yellow (-4 to +4): 100% XP (normal)
        /// - Orange (+5 to +9): 120% XP (challenging)
        /// - Red (+10+): 150% XP (dangerous)
        /// </summary>
        /// <param name="playerLevel">The player's level.</param>
        /// <param name="targetLevel">The target's level.</param>
        /// <returns>XP multiplier (0.1 to 1.5).</returns>
        public static float GetLevelDifferenceMultiplier(int playerLevel, int targetLevel)
        {
            int diff = targetLevel - playerLevel;

            if (diff <= -10) return 0.1f;   // Grey - almost no XP
            if (diff <= -5) return 0.5f;    // Green - reduced XP
            if (diff <= 4) return 1.0f;     // Yellow - full XP
            if (diff <= 9) return 1.2f;     // Orange - bonus XP
            return 1.5f;                     // Red - high bonus
        }

        #endregion

        #region Display Utilities

        /// <summary>
        /// Format level for display.
        /// </summary>
        /// <param name="level">The level to format.</param>
        /// <param name="starred">Whether to use starred format (for elite creatures).</param>
        /// <returns>Formatted level string.</returns>
        public static string FormatLevel(int level, bool starred = false)
        {
            return starred ? $"\u2605{level}" : $"Lv.{level}";
        }

        /// <summary>
        /// Get color for level difference display.
        /// Used for nameplates to indicate difficulty.
        /// </summary>
        /// <param name="playerLevel">The player's level.</param>
        /// <param name="targetLevel">The target's level.</param>
        /// <returns>Color indicating difficulty.</returns>
        public static Color GetLevelColor(int playerLevel, int targetLevel)
        {
            int diff = targetLevel - playerLevel;

            if (diff <= -10) return Color.gray;                        // Grey - trivial
            if (diff <= -5) return Color.green;                        // Green - easy
            if (diff <= 4) return Color.yellow;                        // Yellow - normal
            if (diff <= 9) return new Color(1f, 0.5f, 0f);            // Orange - challenging
            return Color.red;                                          // Red - dangerous
        }

        /// <summary>
        /// Format XP amount for display with suffixes (K, M, B).
        /// </summary>
        /// <param name="xp">XP amount.</param>
        /// <returns>Formatted string.</returns>
        public static string FormatXP(long xp)
        {
            if (xp < 1000) return xp.ToString();
            if (xp < 1000000) return $"{xp / 1000.0:F1}K";
            if (xp < 1000000000) return $"{xp / 1000000.0:F1}M";
            return $"{xp / 1000000000.0:F1}B";
        }

        #endregion
    }
}
