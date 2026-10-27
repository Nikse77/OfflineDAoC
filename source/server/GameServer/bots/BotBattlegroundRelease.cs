using System.Numerics;
using DOL.Database;

namespace DOL.GS
{
    /// <summary>
    /// Owner 2026-10-27: a gamebot that dies in a battleground it still fits releases at its realm's portal keep
    /// there, as players do with bg_release_to_portal_keep, instead of in its capital: the battlegrounds have no
    /// bind stones, so it used to wake in Camelot, Jordheim or Tir na Nog and take the teleporter back. Companions,
    /// out-levelled bots and every other region keep the normal release.
    /// </summary>
    public static class BotBattlegroundRelease
    {
        public static bool Applies(bool autonomous, ushort deathRegion, int level) =>
            autonomous && BattlegroundBrackets.ForRegion(deathRegion) is { } bracket && BattlegroundBrackets.Allows(bracket, level);

        /// <summary>The realm's portal-keep arrival point on the mesh floor, spread a little by <paramref name="slot"/>.</summary>
        public static Point3D Point(ushort deathRegion, eRealm realm, int slot)
        {
            BattlegroundBrackets.Bracket bracket = BattlegroundBrackets.ForRegion(deathRegion);
            GameLocation arrival = bracket == null ? null : BattlegroundBrackets.BotArrival(bracket, realm);
            if (arrival == null) return null;
            Vector3 floor = AutonomousTownTeleporters.Arrival(
                new DbTeleport { RegionID = bracket.RegionId, X = arrival.X, Y = arrival.Y, Z = arrival.Z }, slot);
            return new Point3D((int)floor.X, (int)floor.Y, (int)floor.Z);
        }
    }
}
