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
