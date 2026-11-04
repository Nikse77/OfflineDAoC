namespace DOL.GS
{
    /// <summary>
    /// How often a marching follower that is far from its place in the column replans its route (owner 2026-11-04). It used to
    /// do so on every AI turn: about 170 ms each, and 213 of the 235 slow NPC ticks of an hour were marching bots.
    /// </summary>
    public static class AutonomousMarchRepath
    {
        /// <summary>The shortest time between two replans of a follower that keeps walking.</summary>
        public const long IntervalMilliseconds = 8_000;

        /// <summary>The spread added per bot so a whole column does not replan in the same tick.</summary>
        public const int SpreadMilliseconds = 2_000;

        /// <summary>A bot that stands still replans at once; one that walks waits for its interval.</summary>
        public static bool ShouldReplan(bool moving, long now, long nextAllowed) => !moving || now >= nextAllowed;

        /// <summary>The wait before the next replan of this bot: the interval plus a spread of its own.</summary>
        public static long NextDelayMilliseconds(int objectId) => IntervalMilliseconds + (uint)objectId % SpreadMilliseconds;
    }
}
