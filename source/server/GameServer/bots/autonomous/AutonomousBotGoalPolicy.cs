using System;
using System.IO;
using OfflineDaoc.Configuration;

namespace DOL.GS;

public static class AutonomousBotGoalPolicy
{
    public static BotGoalSettings Settings { get; private set; } = BotGoalSettings.Defaults;
    public const string OverrideVariable = "OFFLINE_DAOC_BOT_GOALS";
    public static bool IsConfigured { get; private set; }

    // One startup read only. Never poll settings or query the database on AI turns.
    public static void Initialize(string serverDirectory)
    {
        string path = Path.Combine(serverDirectory, BotGoalSettings.FileName);
        // Test runs may point at their own goals file so the owner's saved launcher setting
        // (bot-goals.json) is never edited for a test.
        string overridePath = Environment.GetEnvironmentVariable(OverrideVariable);
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
        {
            path = overridePath;
            DOL.Logging.LoggerManager.Create(typeof(AutonomousBotGoalPolicy)).Warn($"BOT_GOALS_OVERRIDE using {overridePath} instead of {BotGoalSettings.FileName}");
        }
        Settings = BotGoalSettings.Load(path); // Reject corruption rather than silently ignoring 0% exclusions.
        IsConfigured = File.Exists(path);
    }

    // Owner 2026-10-27: gamebots of levels 1-6 never group, whatever the Bot
    // Goals Setting says. They take solo PvE instead of group PvE.
    public const int SoloOnlyMaximumLevel = 6;

    public static bool GroupsAllowed(int level) => level > SoloOnlyMaximumLevel;

    public static eAutonomousObjectiveKind Choose(int level, Random random = null, bool excludeGroup = false,
        bool excludeBattlegrounds = false)
    {
        var kind = (eAutonomousObjectiveKind)Settings.ForLevel(level).Choose((random ?? Random.Shared).NextDouble(),
            excludeGroup || !GroupsAllowed(level), excludeBattlegrounds || !BattlegroundBrackets.LevelHasBracket(level));
        // A group-only row still answers group PvE with the group share excluded.
        return kind == eAutonomousObjectiveKind.GroupPve && !GroupsAllowed(level) ? eAutonomousObjectiveKind.SoloPve : kind;
    }

    // A battleground goal also needs a bracket for the level (15-35); level is the only battleground limit.
    public static bool Allows(int level, eAutonomousObjectiveKind kind) =>
        Settings.ForLevel(level).Allows((int)kind) &&
        (kind != eAutonomousObjectiveKind.GroupPve || GroupsAllowed(level)) &&
        (kind != eAutonomousObjectiveKind.Battleground || BattlegroundBrackets.LevelHasBracket(level));

    public static eAutonomousObjectiveKind EnsureAllowed(int level, eAutonomousObjectiveKind kind) =>
        Allows(level, kind) ? kind : Choose(level);

    // Called before an autonomous actor can enter the world. Keep inventory,
    // position, XP and valid saved task clocks; discard only a now-disabled task.
    public static bool ReconcileSavedAssignment(OfflineWorldBotRecord record)
    {
        if (!IsConfigured || record == null) return false;
        bool changed = false;
        if (!string.IsNullOrEmpty(record.ObjectiveRvrEligibleUtc))
        {
            record.ObjectiveRvrEligibleUtc = string.Empty;
            changed = true;
        }
        if (!AutonomousObjectiveAssignments.IsBetweenPveTasks(record) &&
            !Allows(record.Level, AutonomousObjectiveAssignments.Parse(record.ObjectiveKind)))
        {
            record.ObjectiveKind = Choose(record.Level).ToString();
            record.ObjectiveAssignmentId = string.Empty;
            record.ObjectiveAssignedUtc = record.ObjectiveExpiresUtc = string.Empty;
            record.CurrentCampId = record.TargetName = record.TravelDestination = string.Empty;
            record.ObjectivePhase = "Choosing a goal using Bot Goals Setting";
            changed = true;
        }
        if (changed) record.Dirty = true;
        return changed;
    }
}
