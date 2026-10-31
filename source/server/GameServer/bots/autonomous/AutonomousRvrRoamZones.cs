using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS;

/// <summary>
/// Owner 2026-10-31: when the level-50 RvR bots have no siege, defense or relic to go to, the random zone they patrol or hunt
/// in is one of the three zones of the old frontiers that hold the portal keeps: Hadrian's Wall (region 1, zone 15), Odin's
/// Gate (region 100, zone 115) and Emain Macha (region 200, zone 214). The zones are read from the portal keeps themselves,
/// so a different map follows its own keeps. Hunting an enemy force is not limited to them. With no keep data (unit tests, a map without portal keeps) nothing is restricted.
/// </summary>
public static class AutonomousRvrRoamZones
{
    private static readonly ConcurrentDictionary<ushort, HashSet<ushort>> Cache = new();

    /// <summary>True when a patrol may take place at this point (or when no portal keep zone is known for the region).</summary>
    public static bool IsRoamPoint(ushort regionId, int x, int y)
    {
        HashSet<ushort> zones = ZonesOf(regionId);
        if (zones == null)
            return true;
        Zone zone = WorldMgr.GetRegion(regionId)?.GetZone(x, y);
        return zone != null && zones.Contains(zone.ID);
    }

    // Owner 2026-10-31, step 3 of the RvR plan: the patrol zone of each force, spread equally over the three zones per realm.
    // The zones are taken in this order: Emain Macha (region 200) first, Odin's Gate (100) second, Hadrian's Wall (1) third.
    public static readonly ushort[] PatrolOrder = [200, 100, 1];
    // A safety net only: a force keeps its zone until its group ends (Release), but a bot on its own never ends one, and an
    // entry nobody has asked for in four hours (longer than any tenure) belongs to a force that is gone.
    private const long AssignmentMemoryMilliseconds = 4 * 60 * 60_000;
    private sealed record Patrol(eRealm Realm, ushort Region, long SeenTick);
    private static readonly ConcurrentDictionary<string, Patrol> Patrols = new();

    /// <summary>
    /// The region (that is, the zone of its portal keeps) a force patrols. A force chooses once and stays there, through any siege,
    /// defense or relic it is called to, until its group ends (<see cref="Release"/>); a new force gets the zone with the fewest
    /// forces of its realm, the first of <see cref="PatrolOrder"/> on a tie.
    /// </summary>
    public static ushort PatrolRegionFor(string forceId, eRealm realm, long nowTick)
    {
        foreach (var old in Patrols.Where(entry => nowTick - entry.Value.SeenTick > AssignmentMemoryMilliseconds).ToArray())
            Patrols.TryRemove(old.Key, out _);
        if (Patrols.TryGetValue(forceId, out Patrol known))
        {
            Patrols[forceId] = known with { SeenTick = nowTick };
            return known.Region;
        }
        var counts = new Dictionary<ushort, int>();
        foreach (ushort region in PatrolOrder)
            counts[region] = Patrols.Values.Count(patrol => patrol.Realm == realm && patrol.Region == region);
        ushort chosen = LeastUsed(PatrolOrder, counts);
        Patrols[forceId] = new Patrol(realm, chosen, nowTick);
        return chosen;
    }

    /// <summary>The group (or force) is over: its zone is free again for the next one.</summary>
    public static void Release(string forceId) => Patrols.TryRemove(forceId, out _);

    /// <summary>The first region of the order with the fewest forces.</summary>
    public static ushort LeastUsed(IReadOnlyList<ushort> order, IReadOnlyDictionary<ushort, int> counts)
    {
        ushort best = order[0];
        foreach (ushort region in order)
            if (counts.GetValueOrDefault(region) < counts.GetValueOrDefault(best))
                best = region;
        return best;
    }

    /// <summary>True when the force patrols this point: its own zone, or any point when the zones are not known.</summary>
    public static bool IsPatrolPoint(string forceId, eRealm realm, long nowTick, ushort regionId, int x, int y)
    {
        if (ZonesOf(regionId) == null)
            return true;
        return regionId == PatrolRegionFor(forceId, realm, nowTick) && IsRoamPoint(regionId, x, y);
    }

    /// <summary>The zone ids of a region's portal keeps; null when none can be read.</summary>
    private static HashSet<ushort> ZonesOf(ushort regionId)
    {
        if (regionId is not (1 or 100 or 200))
            return null;
        if (Cache.TryGetValue(regionId, out HashSet<ushort> known))
            return known;
        Region region = WorldMgr.GetRegion(regionId);
        IEnumerable<DOL.GS.Keeps.AbstractGameKeep> keeps = null;
        try { keeps = GameServer.Instance == null ? null : GameServer.KeepManager?.GetKeepsOfRegion(regionId); }
        catch (System.Exception) { keeps = null; } // no running server (unit tests): nothing is restricted
        if (region == null || keeps == null)
            return null;
        HashSet<ushort> zones = keeps.Where(keep => keep.IsPortalKeep)
            .Select(keep => region.GetZone(keep.X, keep.Y)?.ID).Where(id => id.HasValue).Select(id => id.Value).ToHashSet();
        if (zones.Count == 0)
            return null;
        Cache[regionId] = zones;
        return zones;
    }
}
