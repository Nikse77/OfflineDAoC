using System;

namespace DOL.GS
{
    /// <summary>
    /// Owner 2026-10-30: a small random delay before a bot reacts in a fight, so it looks less like a machine. Two
    /// reactions: the first move of a new fight (<see cref="Engage"/>), the first heal once someone is hurt
    /// (<see cref="Heal"/>) and turning on a new attacker while already fighting (<see cref="Retarget"/>: an arrow
    /// hits and the bot does not swing round the same instant). Each is rolled once per fight or per injury, per bot, not every turn; the bot keeps
    /// doing whatever else it does meanwhile (a healer attacks, a fighter waits).
    /// </summary>
    public static class BotReactionDelay
    {
        public const int EngageMinimumMilliseconds = 250;
        public const int EngageMaximumMilliseconds = 700;
        public const int HealMinimumMilliseconds = 200;
        public const int HealMaximumMilliseconds = 500;
        public const int RetargetMinimumMilliseconds = 400;
        public const int RetargetMaximumMilliseconds = 1000;

        public static readonly (int Minimum, int Maximum) Engage = (EngageMinimumMilliseconds, EngageMaximumMilliseconds);
        public static readonly (int Minimum, int Maximum) Heal = (HealMinimumMilliseconds, HealMaximumMilliseconds);
        public static readonly (int Minimum, int Maximum) Retarget = (RetargetMinimumMilliseconds, RetargetMaximumMilliseconds);

        /// <summary>Off only in the unit tests, whose bots must act at once.</summary>
        public static bool Enabled = true;

        /// <summary>The delay a bot uses now: <see cref="Roll"/>, or 0 while <see cref="Enabled"/> is off.</summary>
        public static int Next((int Minimum, int Maximum) range) => Enabled ? Roll(range) : 0;

        /// <summary>A random delay in milliseconds, both ends included.</summary>
        public static int Roll((int Minimum, int Maximum) range, Random random = null) =>
            (random ?? Random.Shared).Next(range.Minimum, range.Maximum + 1);

        /// <summary>True while the armed reaction time has not come yet.</summary>
        public static bool StillReacting(long now, long readyAt) => readyAt != 0 && now < readyAt;
    }
}
