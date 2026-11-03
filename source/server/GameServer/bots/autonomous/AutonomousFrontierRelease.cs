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
        /// The rally point of every keep, home or enemy, is the frontier hastener standing outside it: its own spot
        /// plus five hundred units along the direction it faces. Owner 2026-11-02: "la posizione dell'hastener più 500
        /// punti nella direzione in cui guarda l'hastener"; for Emain Albion the direction he read in game (319°) is the
        /// one used, since the stored heading pointed the other way.
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
            (eRealm.Albion, 200) => new GateSpot(474679, 342496, 4080, -0.7045, -0.7097),
            (eRealm.Albion, 100) => new GateSpot(597274, 629875, 5971, 0.4868, -0.8735),
            (eRealm.Midgard, 1) => new GateSpot(654013, 294113, 4879, -0.7981, 0.6026),
            (eRealm.Midgard, 200) => new GateSpot(473000, 296290, 3871, -0.7123, 0.7019),
            (eRealm.Hibernia, 1) => new GateSpot(606664, 295125, 4839, 0.5366, 0.8439),
            (eRealm.Hibernia, 100) => new GateSpot(596891, 582904, 6031, 0.4858, 0.8741),
            // Owner 2026-11-02: "tiri una linea dal centro del pad all'hastener e siccome l'hastener sta davanti al keep
            // sai in quale direzione devi aggiungere i 500 punti". The pad-to-hastener line is the outward direction, so
            // the five hundred are added past the hastener along it, home keeps included.
            // Owner 2026-11-03: the hastener's own spot (586460,476519) is a navigation pocket of the installed mesh - a path from
            // it reaches 46,000 units short of Caer Renaris, 403 route queries failed there and Albion regroups stood in it.
            // This point on the connected ground south of the keep (probe UT_HomeAlbGateFrontNavigation): the whole fan of
            // GateOffset (30 to 650 forward, 400 sideways) joins the frontier network, 63 of 63 sampled points.
            (eRealm.Albion, 1) => new GateSpot(585685, 475450, 2602, 0.8660, -0.5000),
            (eRealm.Midgard, 100) => new GateSpot(766219, 667345, 5736, -0.2533, -0.9674),
            (eRealm.Hibernia, 200) => new GateSpot(332862, 418660, 5184, -0.7339, -0.6793),
            _ => null,
        };
    }
}
