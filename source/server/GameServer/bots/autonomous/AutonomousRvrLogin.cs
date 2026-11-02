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

    /// <summary>
    /// The login point: the realm's home frontier keep, on the passage pad itself (owner 2026-11-01), spread around the
    /// teleporters by bot so that they do not stack.
    /// </summary>
    public static bool TryGetPoint(eRealm realm, long botId, out ushort region, out Vector3 point)
    {
        region = 0;
        point = default;
        ushort home = RealmRaidNeutralEvents.HomeRegion(realm);
        AutonomousFrontierTransport.Passage passage = AutonomousFrontierTransport.Destination(realm, home);
        if (passage?.Location == null)
            return false;
        region = passage.Location.RegionID;
        ushort loginRegion = region;
        // The pad the home necklace lands on, kept as the fallback when the porter's pad has no room to spread bots.
        Vector3 necklace = new(passage.Location.X, passage.Location.Y, passage.Location.Z);
        point = necklace;
        // Owner 2026-11-01: on the passage pad, where the player stands next to the realm's frontier porter (Master
        // Visur at Castle Sauvage), not on the pad the necklace lands on about 1,700 units away.
        if (AutonomousFrontierTransport.TryGetHomePad(realm, home, out Vector3 pad))
            point = pad;
        Vector3 centre = point;
        // Owner 2026-11-02: "anche stavolta alb non ha loggato a castel sauvage". An out parameter is assigned even when
        // the call fails, so a failed spread used to wipe the good necklace and leave the point at 0,0,0 - 33 of Albion's
        // 47 level-50 bots landed there on every boot while Midgard and Hibernia found their zone. The necklace is a
        // hand-written coordinate, so it is the floor this can never fall below.
        Vector3 spread = necklace;
        if (!TrySpread(centre, out spread) && (centre == necklace || !TrySpread(necklace, out spread)))
            spread = necklace;
        point = spread;
        return true;

        // 220 to 410 units from the centre, on floor the navmesh accepts: the bots stand around the pad, not on one spot.
        bool TrySpread(Vector3 around, out Vector3 spread)
        {
            spread = around;
            Zone zone = WorldMgr.GetRegion(loginRegion)?.GetZone((int)around.X, (int)around.Y);
            return zone != null &&
                AutonomousFrontierTransport.TryWaitingPoint(PathfindingProvider.Instance, zone, around, realm, botId, out spread);
        }
    }

    /// <summary>The passage pad of the realm's frontier porter, when that porter is in the world.</summary>
    public static Vector3? Pad(eRealm realm) =>
        AutonomousFrontierTransport.TryGetHomePad(realm, RealmRaidNeutralEvents.HomeRegion(realm), out Vector3 pad) ? pad : null;
}
