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
}
