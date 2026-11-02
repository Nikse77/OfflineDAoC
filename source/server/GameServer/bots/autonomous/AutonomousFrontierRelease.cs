using System;
using System.Numerics;

namespace DOL.GS
{
    /// <summary>
    /// Owner 2026-11-02: "la release di un bot in frontiera (sia bot rvr che bot pve) deve SEMPRE seguire queste regole:
    /// se sono nella mia frontiera la release avviene al mio border keep, quello che ha il portale; se sono in una
    /// frontiera avversaria la release avviene sempre al mio border keep in quella frontiera dove c'è il portale da dove
    /// sono entrato".
    ///
    /// The point is the teleporter's own landing pad: for every realm and every frontier there is exactly one, the keep
    /// whose porter carries that realm's medallions, so the released member stands where it can cross again at once.
    /// The bindstone cannot answer this in a frontier (the six Albion stones in Emain lie outside every zone and were
    /// rejected), and the border-keep fallback only knows the keeps of the home frontier, which sent an Emain casualty
    /// back to Castle Sauvage in Hadrian's Wall.
    /// </summary>
    public static class AutonomousFrontierRelease
    {
        /// <summary>
        /// Where <paramref name="realm"/> releases in <paramref name="region"/>: its own frontier's keep with the
        /// portal, or its portal keep in the enemy frontier it entered from.
        /// </summary>
        public static bool TryPoint(eRealm realm, ushort region, out ushort releaseRegion, out Point3D point)
        {
            releaseRegion = 0;
            point = null;
            if (realm is not (eRealm.Albion or eRealm.Midgard or eRealm.Hibernia))
                return false;
            if (region is not (1 or 100 or 200))
                return false;
            AutonomousFrontierTransport.Passage passage = AutonomousFrontierTransport.Destination(realm, region);
            if (passage == null)
                return false;
            releaseRegion = region;
            if (!TryGateFront(realm, region, out point))
                point = new Point3D((int)passage.Location.X, (int)passage.Location.Y, (int)passage.Location.Z);
            return true;
        }

        /// <summary>True when this region is a frontier, so the rule above decides the release.</summary>
        public static bool Applies(ushort region) => region is 1 or 100 or 200;

        /// <summary>
        /// True when the zone is one of the frontier maps. The regions also carry the homeland - Cotswold in region 1,
        /// Mularn in 100, Mag Mell in 200 - so the region alone would send a release at a starting village to a keep.
        /// </summary>
        public static bool IsFrontierZone(ushort region, ushort zone) => region switch
        {
            1 => zone is 11 or 12 or 14 or 15,
            100 => zone is 111 or 112 or 113 or 115,
            200 => zone is 210 or 211 or 212 or 214,
            _ => false,
        };

        /// <summary>
        /// Owner 2026-11-02: "ai border keep nelle frontiere nemiche, quindi hadrian, odin e emain, i pg fermi in attesa
        /// devono stare nella zona fuori di fronte al portone del keep, no intorno o dietro".
        ///
        /// The six entry keeps of Hadrian, Odin and Emain are real Keep rows with doors modelled next to them, so the
        /// spot in front of the gate is the nearest door taken from the keep centre and continued three hundred and
        /// fifty units further out. The three home keeps - Castle Sauvage, Svasud Faste, Druim Ligen - are not Keep
        /// rows and the owner asked to leave them on the porter's pad, so they keep the passage point above.
        /// </summary>
        public static bool TryGateFront(eRealm realm, ushort region, out Point3D point)
        {
            point = null;
            GateSpot? spot = Spot(realm, region);
            if (spot == null)
                return false;
            point = new Point3D(spot.Value.X, spot.Value.Y, spot.Value.Z);
            return true;
        }

        /// <summary>
        /// Owner 2026-11-02: "puoi fare in modo che non si ammassino tutti nello stesso punto ... ma siano un po'
        /// sparpagliati lì fuori". The spot is the gate, the direction below points away from the keep, so the stand can
        /// be a wide fan on the open ground outside the gate - never a pile on one coordinate and never behind the walls.
        /// </summary>
        public static Vector3 GateOffset(eRealm realm, ushort region, long botId, int attempt = 0)
        {
            GateSpot? spot = Spot(realm, region);
            if (spot == null)
                return Vector3.Zero;
            ulong seed = unchecked((ulong)botId * 11400714819323198485UL) ^ (ulong)attempt * 0x9E3779B97F4A7C15UL;
            double across = ((seed >> 20) % 2001) / 1000.0 - 1.0;   // -1 .. 1 sideways
            double outwards = ((seed >> 44) % 1001) / 1000.0;        //  0 .. 1 away from the gate
            float forward = 30 + (float)outwards * 620;
            float lateral = (float)across * 400;
            double dx = spot.Value.Dx, dy = spot.Value.Dy;
            return new((float)(dx * forward - dy * lateral), (float)(dy * forward + dx * lateral), 0);
        }

        private readonly record struct GateSpot(int X, int Y, int Z, double Dx, double Dy);

        private static GateSpot? Spot(eRealm realm, ushort region) => (realm, region) switch
        {
            (eRealm.Albion, 200) => new GateSpot(474_803, 343_226, 4146, -0.9112, -0.4114),
            (eRealm.Albion, 100) => new GateSpot(596_596, 630_190, 6038, 0.1860, -0.9825),
            (eRealm.Midgard, 1) => new GateSpot(654_516, 293_941, 4858, -0.6859, 0.7278),
            (eRealm.Midgard, 200) => new GateSpot(473_466, 296_048, 3848, -0.6191, 0.7853),
            (eRealm.Hibernia, 1) => new GateSpot(606_409, 294_333, 4816, 0.8332, 0.5528),
            (eRealm.Hibernia, 100) => new GateSpot(596_723, 582_126, 6008, 0.7389, 0.6739),
            // Owner 2026-11-02: the three home keeps - Castle Sauvage, Svasud Faste, Druim Ligen - keep their pad. A gate
            // front computed at 2.500 units from the keep centre was not walkable: nobody reached it, every member
            // reported an unreachable rendezvous and the party bounced between frontiers (327 crossings, 53 reselects in
            // 27 minutes against 37 and 1 before). The pad is where the bots land, so it is walkable by definition.
            _ => null,
        };
    }
}
