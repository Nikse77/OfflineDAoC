using System.Runtime.CompilerServices;
using DOL.Database;

namespace DOL.GS;

/// <summary>
/// Owner 2026-11-01: a level-50 RvR bot patrols the frontiers and must never be doing PvE content. A run found ten
/// level-50 Albion bots in Marfach Caverns and the Hall of the Corrupt, dying to the Black Lady at level 65 and pulling
/// Aidon the Archwizard at level 75: a dungeon camp (the level-50 group dungeon preference) had routed a frontier party
/// into a dungeon, and bots do not cross a zone point on their own - the party's camp is where the route comes from.
/// The guard therefore has two halves: no dungeon camp is offered to an RvR party (ShouldStayOutOfDungeons), and any
/// RvR bot still found inside is sent back to its realm's frontier keep with the marker RVR_DUNGEON_DIVERSION, which
/// records where it came from. Owner 2026-11-01: the frontier dungeons that lead to Summoner's Hall stay open to a group
/// on relic duty (relic keep assault, relic carrier escort) - that is the one reason a frontier group may be inside -
/// and groups that carry a PvE objective are untouched.
/// </summary>
public static class AutonomousRvrDungeonGuard
{
    /// <summary>The regions a level-50 RvR bot is allowed to be in: Hadrian's Wall, Odin's Gate, Emain Macha.</summary>
    public static bool IsPatrolRegion(ushort region) => region is 1 or 100 or 200;

    /// <summary>The frontier dungeons that lead to Summoner's Hall: Dodens Gruva, Marfach Caverns, Hall of the
    /// Corrupt. Named explicitly rather than by Zone.IsDungeon: the capitals Camelot (10), Jordheim (101) and Tir na
    /// Nog (201) also report IsDungeon true, and a frontier party trains and queues for the teleporter there.</summary>
    public static bool IsFrontierDungeon(ushort region) => region is 246 or 276 or 277;

    /// <summary>Pure rule: an RvR bot only fights outdoors, so a dungeon camp is never offered to it. This is the
    /// anticipatory half: with no dungeon camp there is no route into the dungeon in the first place.</summary>
    public static bool ShouldStayOutOfDungeons(eAutonomousObjectiveKind objective, bool onRelicDuty) =>
        objective == eAutonomousObjectiveKind.RvR && !onRelicDuty;

    /// <summary>Pure rule: an RvR bot is sent home from a dungeon unless its group is on a relic operation, which is the
    /// only reason a frontier group has to be inside one.</summary>
    public static bool ShouldReturnToFrontier(eAutonomousObjectiveKind objective, bool onRelicDuty, ushort region) =>
        ShouldStayOutOfDungeons(objective, onRelicDuty) && IsFrontierDungeon(region);

    /// <summary>
    /// Owner 2026-11-01: "escludiamo i dungeon di frontiera da ogni cosa che riguarda l'rvr, tutto, non devono comparire
    /// come percorso o meta mai in nessun caso". Refusing the transfer was not enough: the route finder kept walking
    /// through the dungeon's zone point, the move was refused, the party replanned the same corner and stood there -
    /// 4,457 refusals in one session across 56 bots, whole groups frozen at the Marfach Caverns mouth until something
    /// killed them. The two methods below take the dungeon out of the choices themselves: never a destination, never a
    /// waypoint, for a level-50 RvR party that is not on relic duty.
    /// </summary>
    public static bool ExcludesRegion(GameBot bot, ushort region) =>
        bot != null && IsFrontierDungeon(region) && Excludes(bot);

    /// <summary>A zone point that touches a frontier dungeon is not a route for an RvR party, in either direction.</summary>
    public static bool ExcludesEdge(GameBot bot, DbZonePoint point) =>
        bot != null && point != null &&
        (IsFrontierDungeon(point.SourceRegion) || IsFrontierDungeon(point.TargetRegion)) && Excludes(bot);

    private static bool Excludes(GameBot bot) =>
        bot.Level >= 50 && ShouldStayOutOfDungeons(AutonomousObjectiveAssignments.KindFor(bot),
            AutonomousRvrEventLayer.OnRelicDuty(AutonomousBotGroupCoordinator.RvrForceId(bot)));

    /// <summary>A goal refused at a dungeon edge this many times in a minute is abandoned, not retried.</summary>
    public const int RefusalsBeforeAbandon = 3;

    public const long RefusalWindowMilliseconds = 60_000;

    private sealed class Refusal
    {
        public int Count;
        public long Since;
        public ushort Region;
    }

    private static readonly ConditionalWeakTable<GameBot, Refusal> Refusals = new();

    public static void NoteRefusal(GameBot bot, ushort region, long now)
    {
        if (bot == null)
            return;
        Refusal refusal = Refusals.GetOrCreateValue(bot);
        if (refusal.Since == 0 || refusal.Region != region || now - refusal.Since > RefusalWindowMilliseconds)
        {
            refusal.Region = region;
            refusal.Since = now;
            refusal.Count = 0;
        }
        refusal.Count++;
    }

    /// <summary>Last safety net: a party that keeps being refused the same way is a party standing still.</summary>
    public static bool ShouldAbandonGoal(GameBot bot, long now) =>
        bot != null && Refusals.TryGetValue(bot, out Refusal refusal) &&
        refusal.Count >= RefusalsBeforeAbandon && now - refusal.Since <= RefusalWindowMilliseconds;

    public static void ClearRefusal(GameBot bot)
    {
        if (bot != null)
            Refusals.Remove(bot);
    }
}
