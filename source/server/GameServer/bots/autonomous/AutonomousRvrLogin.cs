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
    public static bool TryGetPoint(eRealm realm, long botId, out ushort region, out Vector3 point) =>
        TryGetPoint(realm, botId, out region, out point, out _);

    /// <summary>
    /// Same, and <paramref name="source"/> says which branch gave the point: "pad+spread" (spread around the porter's pad),
    /// "floor+spread" (spread around the hard-wired point or the necklace landing) or "hardwired-floor" / "necklace-floor"
    /// (no spread found, the point itself), so a login that lands badly can be told from the log.
    /// </summary>
    public static bool TryGetPoint(eRealm realm, long botId, out ushort region, out Vector3 point, out string source)
    {
        region = 0;
        point = default;
        source = string.Empty;
        ushort home = RealmRaidNeutralEvents.HomeRegion(realm);
        AutonomousFrontierTransport.Passage passage = AutonomousFrontierTransport.Destination(realm, home);
        if (passage?.Location == null)
            return false;
        region = passage.Location.RegionID;
        ushort loginRegion = region;
        Vector3 necklace = new(passage.Location.X, passage.Location.Y, passage.Location.Z);
        // Owner 2026-11-02: "c'è gente dentro i muri a castel sauvage ... al posto di 0,0,0 il centro del pad dove sta
        // master visur, con le coordinate cablate nel codice". The necklace landing sits inside the walls, so both the
        // starting point and the floor below every spread become the frontier porter's own spot.
        Vector3 floor = FixedHomePad(realm) ?? necklace;
        point = floor;
        // Owner 2026-11-01: on the passage pad, where the player stands next to the realm's frontier porter (Master
        // Visur at Castle Sauvage), not on the pad the necklace lands on about 1,700 units away.
        bool padFound = false;
        if (AutonomousFrontierTransport.TryGetHomePad(realm, home, out Vector3 pad))
        {
            point = pad;
            padFound = true;
        }
        Vector3 centre = point;
        // Owner 2026-11-02: "anche stavolta alb non ha loggato a castel sauvage". An out parameter is assigned even when
        // the call fails, so a failed spread used to wipe the good fallback and leave the point at 0,0,0 - 33 of Albion's
        // 47 level-50 bots landed there on every boot while Midgard and Hibernia found their zone.
        Vector3 spread = floor;
        if (TrySpread(centre, out spread))
            source = padFound ? "pad+spread" : "floor+spread";
        else if (centre != floor && TrySpread(floor, out spread))
            source = "floor+spread";
        else
        {
            spread = floor;
            source = FixedHomePad(realm).HasValue ? "hardwired-floor" : "necklace-floor";
        }
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

    /// <summary>
    /// Owner 2026-11-02: the three home frontier porters as read from the world database, so a login never depends on an
    /// NPC being placed already: Master Visur at Castle Sauvage (585995,477187,2623), Stor Gothi Annark at Svasud Faste
    /// (765329,668749,5759), Glasny at Druim Ligen (333733,420740,5207). The necklace landing sits inside the walls.
    /// </summary>
    private static Vector3? FixedHomePad(eRealm realm) => realm switch
    {
        eRealm.Albion => new Vector3(585995, 477187, 2623),
        eRealm.Midgard => new Vector3(765329, 668749, 5759),
        eRealm.Hibernia => new Vector3(333733, 420740, 5207),
        _ => null,
    };

    public static Vector3? Pad(eRealm realm) =>
        AutonomousFrontierTransport.TryGetHomePad(realm, RealmRaidNeutralEvents.HomeRegion(realm), out Vector3 pad) ? pad : null;
}
