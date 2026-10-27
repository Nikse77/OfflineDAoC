using System;
using System.Linq;
using DOL.GS.Keeps;

namespace DOL.GS
{
    /// <summary>
    /// Owner 2026-10-27: a /spawn companion outside a keep that is not its realm's does not chase a target in through
    /// the gate. Near Caer Caledon (neutral, gates down) the melee companions ran after attackers into the keep and
    /// died to its guards. A companion already inside (it went in with its player) fights on as before; outside the
    /// keeps nothing changes. Autonomous gamebots are unchanged.
    /// </summary>
    public static class CompanionKeepChase
    {
        // Only a keep this close can hold the point; the keep's own claim area (3,000) reaches far outside its walls.
        private const int KeepSearchRadius = 2_500;
        private const int DefaultWallRadius = 1_200;
        private const int WallMargin = 50;

        public static bool DropsTarget(bool temporaryCompanion, bool autonomous, bool targetInsideForeignKeep, bool companionInsideSameKeep) =>
            temporaryCompanion && !autonomous && targetInsideForeignKeep && !companionInsideSameKeep;

        public static bool DropsTarget(GameBot companion, GameLiving target)
        {
            if (companion is not { IsTemporaryGroupHelper: true, IsAutonomousWorldBot: false } || target == null ||
                target.CurrentRegionID != companion.CurrentRegionID)
                return false;
            AbstractGameKeep keep;
            // As BattlegroundBrackets does: without a running server (unit tests) there is no keep manager.
            try { keep = GameServer.KeepManager?.GetClosestKeepToSpot(target.CurrentRegionID, target, KeepSearchRadius); }
            catch (Exception) { return false; }
            if (keep == null || keep.Realm == companion.Realm)
                return false;
            return DropsTarget(true, false, IsInside(keep, target), IsInside(keep, companion));
        }

        /// <summary>Inside the walls: nearer the keep's centre than its main gates.</summary>
        public static bool IsInside(AbstractGameKeep keep, GameObject point)
        {
            int wall = keep.Doors.Values
                .Where(door => door != null && door.IsAttackableDoor && !door.IsPostern)
                .Select(door => (int)Math.Sqrt(Math.Pow(door.X - keep.X, 2) + Math.Pow(door.Y - keep.Y, 2)))
                .DefaultIfEmpty(DefaultWallRadius)
                .Max();
            double distance = Math.Sqrt(Math.Pow(point.X - keep.X, 2) + Math.Pow(point.Y - keep.Y, 2));
            return distance < wall - WallMargin;
        }
    }
}
