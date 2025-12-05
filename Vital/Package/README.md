# Vital

Core leveling and player data framework for the Slatyo mod ecosystem.

## Features

- **Server-Authoritative Leveling**: Exponential XP curve with level 100 max
- **Extensible Player Data**: Register custom data types that persist with world saves
- **Automatic Sync**: Data syncs to clients on connect and updates
- **Entity Scaling**: Calculate stat multipliers for any level

## For Mod Developers

### Dependency

Add to your plugin:

```csharp
[BepInDependency("com.slatyo.vital")]
```

### Using the Leveling System

```csharp
using Vital.Core;

// Get player level
int level = VitalLeveling.GetLevel(player);

// Add XP (server only)
VitalLeveling.AddXP(player, 100);

// Get XP for next level
long required = VitalLeveling.GetXPForLevel(level + 1);

// Get stat scaling
float healthMult = VitalScaling.GetMultiplier(level, ScalingStat.Health);
```

### Registering Custom Player Data

```csharp
using Vital.Data;

// Define your data class
public class MyModData : IPlayerData
{
    public int Score { get; set; }
    public List<string> Unlocks { get; set; } = new();

    public void Initialize() { Score = 0; }
    public string Serialize() => JsonUtility.ToJson(this);
    public void Deserialize(string data) => JsonUtility.FromJsonOverwrite(data, this);
    public bool Validate() => Score >= 0;
}

// Register in Awake()
VitalDataStore.Register<MyModData>("mymod");

// Get/Set data (server only for Set)
var data = VitalDataStore.Get<MyModData>(player, "mymod");
data.Score += 10;
VitalDataStore.MarkDirty(player, "mymod"); // Triggers sync

// Listen for changes
VitalDataStore.OnDataChanged += (playerId, moduleId) => { };
VitalDataStore.OnDataSynced += (moduleId) => { }; // Client-side
```

## Requirements

- Valheim 0.217.22+
- BepInEx 5.4.x
- Jotunn 2.20.0+

## Part of the Slatyo Ecosystem

Vital provides shared functionality for:
- **Viking**: Player leveling and progression
- **Denizen**: Creature leveling and scaling
- Future mods: Quest progress, reputation, etc.
