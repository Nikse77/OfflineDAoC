using System.Numerics;

namespace DOL.GS;

/// <summary>
/// Owner 2026-11-01: when the server starts, every level-50 bot enters the world at their realm's frontier keep, the one with the
/// mages who teleport to the other realms' frontiers (Castle Sauvage, Svasud Faste, Druim Ligen: the "Home" passage of each realm),
/// instead of wherever they were saved. They begin where their groups gather.
/// </summary>
public static class AutonomousRvrLogin
{
    public const int Level = 50;

    /// <summary>Every level-50 bot logs in at the frontier keep, whatever its objective (owner 2026-11-01).</summary>
    public static bool Applies(int level) => level == Level;

    /// <summary>The login point: the realm's home frontier keep, spread around the teleporters by bot so that they do not stack.</summary>
    public static bool TryGetPoint(eRealm realm, long botId, out ushort region, out Vector3 point)
    {
        region = 0;
        point = default;
        AutonomousFrontierTransport.Passage passage = AutonomousFrontierTransport.Destination(realm, RealmRaidNeutralEvents.HomeRegion(realm));
        if (passage?.Location == null)
            return false;
        region = passage.Location.RegionID;
        point = new Vector3(passage.Location.X, passage.Location.Y, passage.Location.Z);
        Zone zone = WorldMgr.GetRegion(region)?.GetZone(passage.Location.X, passage.Location.Y);
        if (zone != null && AutonomousFrontierTransport.TryWaitingPoint(PathfindingProvider.Instance, zone, point, realm, botId, out Vector3 spread))
            point = spread;
        return true;
    }
}
