using System.Collections.Generic;

namespace DOL.GS
{
    /// <summary>
    /// The order in which a Paladin casts its instant chants in combat (owner 2026-11-04): armor, damage add, combat heal, endurance
    /// regen, one second between one and the next and four seconds before it starts again; out of combat only the endurance
    /// chant is held. A companion leaves the armor chant out. The chants last six seconds and each has an eight-second reuse
    /// timer, so a chant that is not ready yet is waited for rather than skipped.
    /// </summary>
    public static class BotChantRotation
    {
        /// <summary>Between two chants of the same round.</summary>
        public const long GapMilliseconds = 1_000;

        /// <summary>After the last chant of a round, before the first one is cast again.</summary>
        public const long RoundPauseMilliseconds = 4_000;

        /// <summary>How soon to look again at a chant that is still on its reuse timer.</summary>
        public const long WaitForReuseMilliseconds = 250;

        public static IReadOnlyList<eSpellType> Order(bool companion) => companion
            ? [eSpellType.DamageAdd, eSpellType.CombatHeal, eSpellType.EnduranceRegenBuff]
            : [eSpellType.SpecArmorFactorBuff, eSpellType.DamageAdd, eSpellType.CombatHeal, eSpellType.EnduranceRegenBuff];

        /// <summary>The step after this one, back to the first after the last.</summary>
        public static int NextStep(int step, int count) => count <= 0 ? 0 : (step + 1) % count;

        /// <summary>The wait after casting this step: one second, and the round pause after the last.</summary>
        public static long DelayAfter(int step, int count) => step >= count - 1 ? RoundPauseMilliseconds : GapMilliseconds;
    }
}
