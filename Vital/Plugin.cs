using System;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Utils;
using Vital.Commands;
using Vital.Core;
using Vital.Data;
using Vital.Network;

namespace Vital
{
    /// <summary>
    /// Vital - Core Leveling & Player Data System for Valheim Mod Ecosystem.
    /// Provides shared leveling, XP formulas, stat scaling, and extensible player data storage.
    /// </summary>
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [BepInDependency("com.slatyo.munin", BepInDependency.DependencyFlags.SoftDependency)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class Plugin : BaseUnityPlugin
    {
        /// <summary>Plugin GUID for BepInEx.</summary>
        public const string PluginGUID = "com.slatyo.vital";
        /// <summary>Plugin display name.</summary>
        public const string PluginName = "Vital";
        /// <summary>Plugin version.</summary>
        public const string PluginVersion = "1.0.0";

        /// <summary>Logger instance for Vital.</summary>
        public static ManualLogSource Log { get; private set; }

        /// <summary>Plugin instance.</summary>
        public static Plugin Instance { get; private set; }

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Log.LogInfo($"{PluginName} v{PluginVersion} is loading...");

            // Initialize the leveling system
            Leveling.Initialize();

            // Initialize the player data store
            VitalDataStore.Initialize();

            // Initialize network RPCs
            VitalNetwork.Initialize();

            // Initialize Harmony patches
            _harmony = new Harmony(PluginGUID);
            _harmony.PatchAll();

            // Register Munin commands if available
            RegisterCommands();

            Log.LogInfo($"{PluginName} v{PluginVersion} loaded successfully");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            VitalNetwork.Cleanup();
        }

        private void RegisterCommands()
        {
            if (Chainloader.PluginInfos.ContainsKey("com.slatyo.munin"))
            {
                VitalCommands.Register();
            }
            else
            {
                Log.LogDebug("Munin not found, skipping command registration");
            }
        }

        /// <summary>
        /// Helper method to check if running on server.
        /// </summary>
        public static bool IsServer() => ZNet.instance != null && ZNet.instance.IsServer();

        /// <summary>
        /// Helper method to check if running on client (not server).
        /// </summary>
        public static bool IsClient() => ZNet.instance != null && !ZNet.instance.IsServer();

        /// <summary>
        /// Helper method to check if in single player mode.
        /// </summary>
        public static bool IsSinglePlayer() => ZNet.instance != null && !ZNet.instance.IsDedicated() && ZNet.instance.IsServer();
    }
}
