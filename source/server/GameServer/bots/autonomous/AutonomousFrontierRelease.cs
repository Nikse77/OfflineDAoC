using System;

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
            point = new Point3D((int)passage.Location.X, (int)passage.Location.Y, (int)passage.Location.Z);
            return true;
        }

        /// <summary>True when this region is a frontier, so the rule above decides the release.</summary>
        public static bool Applies(ushort region) => region is 1 or 100 or 200;
    }
}
