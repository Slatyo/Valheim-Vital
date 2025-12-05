# Vital

> Core leveling and player data framework for Valheim mods. The foundation for entity progression and server-authoritative data storage.

Vital provides the foundation for entity leveling, XP progression, stat scaling, and extensible server-authoritative player data storage. It's designed to be used by other ecosystem mods like Viking (players) and Denizen (creatures).

## Features

- **Leveling System** - Universal level storage for players and creatures (1-100)
- **XP Formula** - Exponential curve with prestige scaling at high levels
- **Stat Scaling** - Per-level multipliers for Health, Damage, Armor, Stamina, Eitr
- **Creature Tiers** - Normal, Elite, Boss, World Boss with tier-based scaling
- **Kill XP** - Calculate XP rewards with level difference modifiers
- **Player Data Store** - Extensible, server-authoritative data storage
- **Network Sync** - Automatic client sync on connect and data changes
- **World Persistence** - JSON-based storage with versioning and backups

## Requirements

- [BepInEx 5.4.x](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/)
- [Jotunn 2.20.0+](https://valheim.thunderstore.io/package/ValheimModding/Jotunn/)

### Optional
- [Munin](https://github.com/Slatyo/Valheim-Munin) - For console commands

## Installation

1. Install BepInEx and Jotunn
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

### Leveling - XP and Level Calculations

```csharp
using Vital.Core;

// Get XP required to reach a level
long xpNeeded = Leveling.GetXPForLevel(50);

// Get level from total XP
int level = Leveling.GetLevelForXP(totalXP);

// Calculate kill XP with level difference modifier
long killXp = Leveling.CalculateKillXP(killerLevel: 10, victimLevel: 15, baseXp: 100);

// Get level display string (with color coding)
string display = Leveling.GetLevelDisplay(creature);  // "[Lv.25]"

// Get/set entity level (works for players and creatures)
int level = Leveling.GetLevel(character);
Leveling.SetLevel(creature, 50);
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

### Player Data - Extensible Storage

```csharp
using Vital.Data;

// Define your custom data class
public class MyModData : IPlayerData
{
    public int Score { get; set; } = 0;
    public List<string> Unlocks { get; set; } = new();

    public void Initialize() { }  // Called for new players

    public string Serialize() => JsonUtility.ToJson(this);

    public void Deserialize(string data)
    {
        if (!string.IsNullOrEmpty(data))
            JsonUtility.FromJsonOverwrite(data, this);
    }

    public bool Validate() => true;
}

// Register in your mod's Awake()
VitalDataStore.Register<MyModData>("mymod");

// Access player data (server-authoritative)
var data = VitalDataStore.Get<MyModData>(player, "mymod");
data.Score += 100;
VitalDataStore.MarkDirty(playerId, "mymod");  // Triggers sync to client

// Check if data exists
bool hasData = VitalDataStore.Has(playerId, "mymod");
```

### Server-Authoritative Pattern

```csharp
// IMPORTANT: All writes must go through the server
public void AddScore(Player player, int amount)
{
    // Only execute on server
    if (!ZNet.instance.IsServer())
    {
        // Request server to do it via RPC
        return;
    }

    var data = VitalDataStore.Get<MyModData>(player, "mymod");
    data.Score += amount;
    VitalDataStore.MarkDirty(player.GetPlayerID(), "mymod");
}

// Reading works on both client and server (client has synced cache)
public int GetScore(Player player)
{
    var data = VitalDataStore.Get<MyModData>(player, "mymod");
    return data.Score;
}
```

## Architecture

### Data Flow

```
Server                              Client
  │                                   │
  ├─ VitalDataStore (authoritative)   │
  │   ├─ Validates all writes         │
  │   ├─ Persists to JSON             │
  │   └─ Triggers sync ──────────────►├─ VitalDataStore (cache)
  │                                   │   └─ Read-only access
  │                                   │
  └─ World Save ◄─────────────────────┘
      └─ {SaveFolder}/Vital/{World}/playerdata.json
```

### Persistence Format

```json
{
  "version": 1,
  "savedAt": "2024-01-15T10:30:00Z",
  "modules": {
    "vital.levels": {
      "12345678": { "Level": 50, "TotalXP": 1500000 }
    },
    "mymod": {
      "12345678": { "Score": 100, "Unlocks": ["item1", "item2"] }
    }
  }
}
```

## Complete Example

```csharp
using BepInEx;
using Vital.Core;
using Vital.Data;

[BepInPlugin("com.author.mymod", "MyMod", "1.0.0")]
[BepInDependency("com.slatyo.vital")]
public class MyMod : BaseUnityPlugin
{
    public class MyData : IPlayerData
    {
        public int Kills { get; set; } = 0;

        public void Initialize() { }
        public string Serialize() => JsonUtility.ToJson(this);
        public void Deserialize(string data) => JsonUtility.FromJsonOverwrite(data, this);
        public bool Validate() => Kills >= 0;
    }

    private void Awake()
    {
        // Register custom data
        VitalDataStore.Register<MyData>("mymod.kills");

        Logger.LogInfo("MyMod initialized with Vital");
    }

    public void OnCreatureKilled(Player killer, Character victim)
    {
        if (!ZNet.instance.IsServer()) return;

        // Get killer's level
        int killerLevel = Leveling.GetLevel(killer);
        int victimLevel = Leveling.GetLevel(victim);

        // Calculate and award XP
        long xp = Leveling.CalculateKillXP(killerLevel, victimLevel, baseXp: 50);

        // Update kill count
        var data = VitalDataStore.Get<MyData>(killer, "mymod.kills");
        data.Kills++;
        VitalDataStore.MarkDirty(killer.GetPlayerID(), "mymod.kills");
    }
}
```

## License

MIT License - See LICENSE file for details.
