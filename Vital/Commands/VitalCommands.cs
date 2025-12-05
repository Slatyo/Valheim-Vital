using Munin;
using Vital.Core;

namespace Vital.Commands
{
    /// <summary>
    /// Console commands for testing Vital systems.
    /// </summary>
    internal static class VitalCommands
    {
        /// <summary>
        /// Register all Vital commands with Munin.
        /// </summary>
        internal static void Register()
        {
            Command.Register("vital", new CommandConfig
            {
                Name = "status",
                Description = "Show your current level and XP",
                Handler = CmdStatus
            });

            Command.Register("vital", new CommandConfig
            {
                Name = "addxp",
                Description = "Add XP to yourself",
                Usage = "<amount>",
                Permission = PermissionLevel.Admin,
                Examples = new[] { "100", "1000" },
                Handler = CmdAddXP
            });

            Command.Register("vital", new CommandConfig
            {
                Name = "setlevel",
                Description = "Set your level directly",
                Usage = "<level>",
                Permission = PermissionLevel.Admin,
                Examples = new[] { "10", "50", "100" },
                Handler = CmdSetLevel
            });

            Command.Register("vital", new CommandConfig
            {
                Name = "xpinfo",
                Description = "Show XP requirements for levels",
                Usage = "[level]",
                Examples = new[] { "", "50", "100" },
                Handler = CmdXPInfo
            });

            Plugin.Log.LogInfo("Vital commands registered with Munin");
        }

        private static CommandResult CmdStatus(CommandArgs args)
        {
            var player = args.Player;
            if (player == null)
                return CommandResult.Error("No player found");

            int level = Leveling.GetLevel(player);
            long currentXP = Leveling.GetXP(player);

            // Use cumulative XP thresholds for progress calculation
            long xpAtCurrentLevel = Leveling.GetCumulativeXP(level);      // Total XP to reach current level
            long xpAtNextLevel = Leveling.GetCumulativeXP(level + 1);     // Total XP to reach next level
            long xpIntoLevel = currentXP - xpAtCurrentLevel;              // XP earned since reaching current level
            long xpNeededForNext = xpAtNextLevel - xpAtCurrentLevel;      // XP needed to go from current to next
            float progress = xpNeededForNext > 0 ? (float)xpIntoLevel / xpNeededForNext * 100f : 100f;

            var lines = new[]
            {
                $"<color=#FFD700>Vital Status</color>",
                $"Level: {level} / {Leveling.MaxLevel}",
                $"Total XP: {currentXP:N0}",
                $"Progress to {level + 1}: {progress:F1}% ({xpIntoLevel:N0} / {xpNeededForNext:N0})"
            };

            return CommandResult.Info(string.Join("\n", lines));
        }

        private static CommandResult CmdAddXP(CommandArgs args)
        {
            var player = args.Player;
            if (player == null)
                return CommandResult.Error("No player found");

            long amount = args.Get<long>(0, 0);
            if (amount <= 0)
                return CommandResult.Error("Usage: munin vital addxp <amount>");

            int oldLevel = Leveling.GetLevel(player);
            Leveling.AddXP(player, amount);
            int newLevel = Leveling.GetLevel(player);
            long newXP = Leveling.GetXP(player);

            if (newLevel > oldLevel)
            {
                return CommandResult.Success($"Added {amount:N0} XP. Level up! {oldLevel} -> {newLevel} (Total XP: {newXP:N0})");
            }

            return CommandResult.Success($"Added {amount:N0} XP. (Total XP: {newXP:N0})");
        }

        private static CommandResult CmdSetLevel(CommandArgs args)
        {
            var player = args.Player;
            if (player == null)
                return CommandResult.Error("No player found");

            int level = args.Get<int>(0, 0);
            if (level < 1 || level > Leveling.MaxLevel)
                return CommandResult.Error($"Usage: munin vital setlevel <1-{Leveling.MaxLevel}>");

            int oldLevel = Leveling.GetLevel(player);
            Leveling.SetLevel(player, level);
            int newLevel = Leveling.GetLevel(player);
            long newXP = Leveling.GetXP(player);

            return CommandResult.Success($"Level changed: {oldLevel} -> {newLevel} (XP: {newXP:N0})");
        }

        private static CommandResult CmdXPInfo(CommandArgs args)
        {
            int targetLevel = args.Get<int>(0, 10);

            if (targetLevel < 1) targetLevel = 1;
            if (targetLevel > Leveling.MaxLevel) targetLevel = Leveling.MaxLevel;

            var lines = new System.Collections.Generic.List<string>
            {
                $"<color=#FFD700>XP Requirements (around level {targetLevel})</color>"
            };

            int start = System.Math.Max(2, targetLevel - 2);
            int end = System.Math.Min(Leveling.MaxLevel, targetLevel + 3);

            for (int i = start; i <= end; i++)
            {
                long cumulativeXP = Leveling.GetCumulativeXP(i);    // Total XP to reach this level
                long incrementalXP = Leveling.GetXPForLevel(i);      // XP needed from previous level
                string marker = i == targetLevel ? " <-" : "";
                lines.Add($"  Level {i,3}: {cumulativeXP,12:N0} total (+{incrementalXP:N0} from {i-1}){marker}");
            }

            return CommandResult.Info(string.Join("\n", lines));
        }
    }
}
