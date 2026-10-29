using System;

namespace DOL.GS.Keeps
{
    /// <summary>
    /// Owner 2026-10-29: the guards and lords of the battleground keeps stood 10 to 25 levels above the bots of their bracket
    /// (Abermenai guards about 28 and lord 33 for bots of 15-19; Caer Caledon guards 48 and lord 57 for bots of 30-35), because
    /// the base level of the keep row plus the keep's upgrade bonus ignores the bracket. In 25 hours of play no battleground keep
    /// changed hands and no lord fell under 95% health. Inside the four battlegrounds (not their portal keeps) a guard is now the
    /// bracket's highest level plus <see cref="ServerProperties.Properties.BG_KEEP_GUARD_LEVEL_OFFSET"/> and a lord plus
    /// <see cref="ServerProperties.Properties.BG_KEEP_LORD_LEVEL_OFFSET"/>. Every other keep keeps the ordinary formula.
    /// </summary>
    public static class BattlegroundKeepLevels
    {
        public static byte Level(int bracketMaxLevel, bool lord, int guardOffset, int lordOffset) =>
            (byte)Math.Clamp(bracketMaxLevel + (lord ? lordOffset : guardOffset), 1, 250);

        /// <summary>True, and the level, for a guard of a central keep in one of the four battlegrounds.</summary>
        public static bool TryFor(AbstractGameKeep keep, GameKeepGuard guard, out byte level)
        {
            level = 0;
            if (!ServerProperties.Properties.BG_KEEP_LEVELS_FROM_BRACKET || keep == null || guard == null || keep.IsPortalKeep ||
                !BattlegroundBrackets.IsBattlegroundRegion(keep.Region))
                return false;
            BattlegroundBrackets.Bracket bracket = BattlegroundBrackets.ForRegion(keep.Region);
            if (bracket == null)
                return false;
            level = Level(bracket.MaxLevel, guard is GuardLord, ServerProperties.Properties.BG_KEEP_GUARD_LEVEL_OFFSET,
                ServerProperties.Properties.BG_KEEP_LORD_LEVEL_OFFSET);
            return true;
        }
    }
}
