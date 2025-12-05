using System;

namespace Vital.Core
{
    /// <summary>
    /// Provides stat scaling multipliers based on entity level.
    /// Used by both Viking (players) and Denizen (creatures) for consistent scaling.
    /// </summary>
    public static class Scaling
    {
        #region Scaling Configuration

        /// <summary>Health bonus per level (percentage as decimal: 0.05 = 5%).</summary>
        public const float HealthPerLevel = 0.05f;

        /// <summary>Damage bonus per level (percentage as decimal: 0.03 = 3%).</summary>
        public const float DamagePerLevel = 0.03f;

        /// <summary>Armor bonus per level (percentage as decimal: 0.02 = 2%).</summary>
        public const float ArmorPerLevel = 0.02f;

        /// <summary>Stamina bonus per level (percentage as decimal: 0.02 = 2%).</summary>
        public const float StaminaPerLevel = 0.02f;

        /// <summary>Eitr bonus per level (percentage as decimal: 0.03 = 3%).</summary>
        public const float EitrPerLevel = 0.03f;

        #endregion

        #region Scaling Multipliers

        /// <summary>
        /// Get health multiplier for given level.
        /// Level 1 = 1.0, Level 100 = 5.95 (with default 5% per level).
        /// </summary>
        /// <param name="level">Entity level.</param>
        /// <returns>Multiplier (1.0 at level 1).</returns>
        public static float GetHealthScaling(int level)
        {
            return GetScalingMultiplier(level, HealthPerLevel);
        }

        /// <summary>
        /// Get damage multiplier for given level.
        /// Level 1 = 1.0, Level 100 = 3.97 (with default 3% per level).
        /// </summary>
        /// <param name="level">Entity level.</param>
        /// <returns>Multiplier (1.0 at level 1).</returns>
        public static float GetDamageScaling(int level)
        {
            return GetScalingMultiplier(level, DamagePerLevel);
        }

        /// <summary>
        /// Get armor multiplier for given level.
        /// Level 1 = 1.0, Level 100 = 2.98 (with default 2% per level).
        /// </summary>
        /// <param name="level">Entity level.</param>
        /// <returns>Multiplier (1.0 at level 1).</returns>
        public static float GetArmorScaling(int level)
        {
            return GetScalingMultiplier(level, ArmorPerLevel);
        }

        /// <summary>
        /// Get stamina multiplier for given level.
        /// Level 1 = 1.0, Level 100 = 2.98 (with default 2% per level).
        /// </summary>
        /// <param name="level">Entity level.</param>
        /// <returns>Multiplier (1.0 at level 1).</returns>
        public static float GetStaminaScaling(int level)
        {
            return GetScalingMultiplier(level, StaminaPerLevel);
        }

        /// <summary>
        /// Get eitr multiplier for given level.
        /// Level 1 = 1.0, Level 100 = 3.97 (with default 3% per level).
        /// </summary>
        /// <param name="level">Entity level.</param>
        /// <returns>Multiplier (1.0 at level 1).</returns>
        public static float GetEitrScaling(int level)
        {
            return GetScalingMultiplier(level, EitrPerLevel);
        }

        /// <summary>
        /// Get a custom scaling multiplier with specified percentage per level.
        /// </summary>
        /// <param name="level">Entity level.</param>
        /// <param name="percentPerLevel">Bonus per level as decimal (0.05 = 5%).</param>
        /// <returns>Multiplier (1.0 at level 1).</returns>
        public static float GetScalingMultiplier(int level, float percentPerLevel)
        {
            level = Math.Max(Leveling.MinLevel, Math.Min(level, Leveling.MaxLevel));
            return 1f + ((level - 1) * percentPerLevel);
        }

        #endregion

        #region Aggregate Scaling

        /// <summary>
        /// Get all scaling values for a level in a single struct.
        /// </summary>
        /// <param name="level">Entity level.</param>
        /// <returns>Struct containing all scaling multipliers.</returns>
        public static LevelScaling GetScaling(int level)
        {
            return new LevelScaling
            {
                Level = level,
                Health = GetHealthScaling(level),
                Damage = GetDamageScaling(level),
                Armor = GetArmorScaling(level),
                Stamina = GetStaminaScaling(level),
                Eitr = GetEitrScaling(level)
            };
        }

        /// <summary>
        /// Get scaling for a Character entity.
        /// </summary>
        /// <param name="entity">The character.</param>
        /// <returns>Struct containing all scaling multipliers.</returns>
        public static LevelScaling GetScaling(Character entity)
        {
            return GetScaling(Leveling.GetLevel(entity));
        }

        #endregion

        #region Creature Tier Scaling

        /// <summary>
        /// Creature tier for additional scaling.
        /// </summary>
        public enum CreatureTier
        {
            /// <summary>Normal creature.</summary>
            Normal = 0,
            /// <summary>Elite creature (starred).</summary>
            Elite = 1,
            /// <summary>Boss creature.</summary>
            Boss = 2,
            /// <summary>World boss / raid boss.</summary>
            WorldBoss = 3
        }

        /// <summary>
        /// Get tier multiplier for creature stats.
        /// </summary>
        /// <param name="tier">Creature tier.</param>
        /// <returns>Multiplier for stats.</returns>
        public static float GetTierMultiplier(CreatureTier tier)
        {
            return tier switch
            {
                CreatureTier.Normal => 1.0f,
                CreatureTier.Elite => 2.0f,
                CreatureTier.Boss => 5.0f,
                CreatureTier.WorldBoss => 10.0f,
                _ => 1.0f
            };
        }

        /// <summary>
        /// Get combined scaling for a creature with level and tier.
        /// </summary>
        /// <param name="level">Creature level.</param>
        /// <param name="tier">Creature tier.</param>
        /// <returns>Combined scaling struct.</returns>
        public static LevelScaling GetCreatureScaling(int level, CreatureTier tier)
        {
            var baseScaling = GetScaling(level);
            float tierMult = GetTierMultiplier(tier);

            return new LevelScaling
            {
                Level = level,
                Health = baseScaling.Health * tierMult,
                Damage = baseScaling.Damage * tierMult,
                Armor = baseScaling.Armor * (tier >= CreatureTier.Elite ? 1.5f : 1.0f),
                Stamina = baseScaling.Stamina,
                Eitr = baseScaling.Eitr
            };
        }

        #endregion
    }

    /// <summary>
    /// Container for all scaling values at a specific level.
    /// </summary>
    public struct LevelScaling
    {
        /// <summary>The level these values are for.</summary>
        public int Level;

        /// <summary>Health multiplier (1.0 at level 1).</summary>
        public float Health;

        /// <summary>Damage multiplier (1.0 at level 1).</summary>
        public float Damage;

        /// <summary>Armor multiplier (1.0 at level 1).</summary>
        public float Armor;

        /// <summary>Stamina multiplier (1.0 at level 1).</summary>
        public float Stamina;

        /// <summary>Eitr multiplier (1.0 at level 1).</summary>
        public float Eitr;

        /// <summary>
        /// Returns a string representation of the scaling values.
        /// </summary>
        public override string ToString()
        {
            return $"Level {Level}: HP x{Health:F2}, DMG x{Damage:F2}, ARM x{Armor:F2}, STA x{Stamina:F2}, EITR x{Eitr:F2}";
        }
    }
}
