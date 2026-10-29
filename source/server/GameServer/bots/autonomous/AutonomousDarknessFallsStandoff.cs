using System;
using System.Collections.Concurrent;
using System.Numerics;
using DOL.Database;

namespace DOL.GS;

/// <summary>
/// Owner 2026-10-29: in Darkness Falls the bots walked the certified route to the end and stood on top of the monster (the
/// route's last point is the spawn itself: 4 units away at the median over all 1,367 certified spawns) and fought from there,
/// in the middle of its pack. A bot now stops on the last point of the certified route that is at least the monster's own aggro
/// range plus <see cref="AggroMargin"/> from the spawn and has a line of sight to it, and the pull is made from there
/// (ranged pull of a party, spell or bow of a single bot). The route up to that point is the same certified one; when no point
/// of the route qualifies the bot walks it to the end as before.
/// </summary>
public static class AutonomousDarknessFallsStandoff
{
    public const int AggroMargin = 400;
    public const int DefaultAggroRange = 500;

    private static readonly ConcurrentDictionary<string, Vector3[]> Cache = new();

    public static float StandoffDistance(int aggroRange) => (aggroRange > 0 ? aggroRange : DefaultAggroRange) + AggroMargin;

    /// <summary>The route cut after its last point that is at least <paramref name="standoff"/> from the spawn with a line of
    /// sight to it; at least two points are kept. The final point (the spawn) never qualifies.</summary>
    public static Vector3[] Truncate(Vector3[] incoming, Vector3 spawn, float standoff, Func<Vector3, bool> hasLineOfSight)
    {
        if (incoming == null || incoming.Length < 3)
            return incoming;
        for (int index = incoming.Length - 2; index >= 1; index--)
            if (Vector3.Distance(incoming[index], spawn) >= standoff && hasLineOfSight(incoming[index]))
                return incoming[..(index + 1)];
        return incoming;
    }

    /// <summary>The standoff route of one certified spawn for one realm; computed once per spawn and realm.</summary>
    public static Vector3[] ForBot(GameBot bot, string mobId, AutonomousDarknessFallsNavigation.SpawnProof proof, Vector3[] incoming)
    {
        if (bot?.CurrentZone == null || proof?.Spawn?.Length != 3 || incoming == null || incoming.Length < 3 ||
            !ServerProperties.Properties.DF_STANDOFF_FROM_AGGRO_RANGE)
            return incoming;
        string key = $"{mobId}:{(int)bot.Realm}";
        if (Cache.TryGetValue(key, out Vector3[] cached))
            return cached;
        IPathfindingMgr nav = PathfindingProvider.Instance;
        if (nav?.IsAvailable != true || !nav.HasNavmesh(bot.CurrentZone))
            return incoming;
        int aggroRange = DefaultAggroRange;
        try { aggroRange = GameServer.Database.FindObjectByKey<DbMob>(mobId)?.AggroRange ?? DefaultAggroRange; }
        catch (Exception) { aggroRange = DefaultAggroRange; }
        Vector3 spawn = new(proof.Spawn[0], proof.Spawn[1], proof.Spawn[2]);
        Zone zone = bot.CurrentZone;
        Vector3[] route = Truncate(incoming, spawn, StandoffDistance(aggroRange),
            point => nav.HasLineOfSight(zone, point, spawn, nav.DefaultFilters));
        Cache[key] = route;
        return route;
    }
}
