# Vital

> Core leveling framework for Valheim mods. The foundation for entity progression and stat scaling.

Vital provides the leveling system for players and creatures. It handles XP progression, level storage, stat scaling, and kill XP calculations. Uses State for player data persistence.

## Features

- **Leveling System** - Universal level storage for players and creatures (1-100)
- **XP Formula** - Exponential curve with prestige scaling at high levels
- **Stat Scaling** - Per-level multipliers for Health, Damage, Armor, Stamina, Eitr
- **Creature Tiers** - Normal, Elite, Boss, World Boss with tier-based scaling
- **Kill XP** - Calculate XP rewards with level difference modifiers

## Requirements

- [BepInEx 5.4.x](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/)
- [Jotunn 2.20.0+](https://valheim.thunderstore.io/package/ValheimModding/Jotunn/)
- [State](https://github.com/Slatyo/Valheim-State) - For player data persistence

### Optional
- [Munin](https://github.com/Slatyo/Valheim-Munin) - For console commands

## Installation

1. Install BepInEx, Jotunn, and State
2. Extract `Vital.dll` to `BepInEx/plugins/`

## Console Commands

Requires [Munin](https://github.com/Slatyo/Valheim-Munin) for console commands:

| Command | Permission | Description |
|---------|------------|-------------|
| `munin vital status` | Anyone | Show your current level and XP |
| `munin vital xpinfo <level>` | Anyone | Show XP required for a level |
| `munin vital addxp <amount>` | Admin | Add XP to your character |
| `munin vital setlevel <level>` | Admin | Set your character level |

## API Reference

### Leveling - Level Storage

```csharp
using Vital.Core;

// Get/set entity level (works for players and creatures)
int level = Leveling.GetLevel(character);
Leveling.SetLevel(creature, 50);

// For players specifically
int playerLevel = Leveling.GetLevel(player);
Leveling.SetLevel(player, 50);

// Add XP to a player (auto-levels up)
Leveling.AddXP(player, 1000);

// Get player's total XP
long xp = Leveling.GetXP(player);

// Get progress to next level (0.0 to 1.0)
float progress = Leveling.GetLevelProgress(player);
```

### Leveling - XP Calculations

```csharp
using Vital.Core;

// Get XP required to reach a level from previous level
long xpNeeded = Leveling.GetXPForLevel(50);

// Get total XP required from level 1 to target level
long cumulative = Leveling.GetCumulativeXP(50);

// Get XP between two levels
long xpBetween = Leveling.GetXPBetweenLevels(10, 20);

// Get level from total XP
int level = Leveling.GetLevelForXP(totalXP);
```

### Leveling - Kill XP

```csharp
using Vital.Core;

// Base kill XP (based on creature level)
long baseXp = Leveling.GetKillXP(creatureLevel: 25);
long eliteXp = Leveling.GetKillXP(creatureLevel: 25, isElite: true);   // 3x
long bossXp = Leveling.GetKillXP(creatureLevel: 25, isBoss: true);     // 10x

// Kill XP with level difference modifier
long xp = Leveling.GetKillXP(playerLevel: 20, creatureLevel: 25);
// Grey (-10+): 10% XP | Green (-5 to -9): 50% XP | Yellow (-4 to +4): 100% XP
// Orange (+5 to +9): 120% XP | Red (+10+): 150% XP

// Get level difference multiplier directly
float mult = Leveling.GetLevelDifferenceMultiplier(playerLevel: 20, targetLevel: 25);
```

### Leveling - Display Utilities

```csharp
using Vital.Core;

// Format level for display
string display = Leveling.FormatLevel(50);           // "Lv.50"
string starred = Leveling.FormatLevel(50, true);     // "★50"

// Get color based on level difference
Color color = Leveling.GetLevelColor(playerLevel: 20, targetLevel: 30);  // Red

// Format XP with suffixes
string formatted = Leveling.FormatXP(1500000);  // "1.5M"
```

### Scaling - Stat Multipliers

```csharp
using Vital.Core;

// Get scaling multipliers for a level
float healthMult = Scaling.GetHealthMultiplier(50);    // 1.0 at level 1, scales up
float damageMult = Scaling.GetDamageMultiplier(50);
float armorMult = Scaling.GetArmorMultiplier(50);

// Get all scaling values at once
var scaling = Scaling.GetScaling(50);
float health = baseHealth * scaling.Health;
float damage = baseDamage * scaling.Damage;

// Creature tier multipliers
float tierMult = Scaling.GetTierMultiplier(CreatureTier.Boss);  // 5.0x
```

### Creature Tiers

```csharp
public enum CreatureTier
{
    Normal,     // 1.0x multiplier
    Elite,      // 2.0x multiplier
    Boss,       // 5.0x multiplier
    WorldBoss   // 10.0x multiplier
}
```

### Events

```csharp
using Vital.Core;

// Called when any entity's level changes
Leveling.OnLevelChanged += (character, oldLevel, newLevel) =>
{
    Plugin.Log.LogInfo($"{character.m_name} leveled: {oldLevel} -> {newLevel}");
};
```

## Complete Example

```csharp
using BepInEx;
using Vital.Core;

[BepInPlugin("com.author.mymod", "MyMod", "1.0.0")]
[BepInDependency("com.slatyo.vital")]
public class MyMod : BaseUnityPlugin
{
    private void Awake()
    {
        // Subscribe to level changes
        Leveling.OnLevelChanged += OnLevelChanged;
        Logger.LogInfo("MyMod initialized with Vital");
    }

    private void OnLevelChanged(Character character, int oldLevel, int newLevel)
    {
        if (character is Player player)
        {
            Logger.LogInfo($"{player.GetPlayerName()} leveled up: {oldLevel} -> {newLevel}!");
        }
    }

    public void OnCreatureKilled(Player killer, Character victim)
    {
        if (!ZNet.instance.IsServer()) return;

        // Get levels
        int killerLevel = Leveling.GetLevel(killer);
        int victimLevel = Leveling.GetLevel(victim);

        // Calculate and award XP
        long xp = Leveling.GetKillXP(killerLevel, victimLevel);
        Leveling.AddXP(killer, xp);

        Logger.LogInfo($"Awarded {Leveling.FormatXP(xp)} XP to {killer.GetPlayerName()}");
    }
}
```

## License

MIT License - See LICENSE file for details.
