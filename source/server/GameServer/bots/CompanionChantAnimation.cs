namespace DOL.GS
{
    /// <summary>
    /// Owner 2026-10-26: a /spawn Paladin's chants send no spell animation. In a
    /// group it twists its chants and recasts one every few seconds, and the
    /// original client draws each group chant's effect round every member, even
    /// with spell effects set to Self. The chants and their bonuses are unchanged.
    /// Human and autonomous Paladins keep their animations.
    /// </summary>
    public static class CompanionChantAnimation
    {
        public static bool Suppresses(bool temporaryCompanion, bool autonomous, eCharacterClass characterClass,
            bool pulsing, bool harmful) =>
            temporaryCompanion && !autonomous && characterClass == eCharacterClass.Paladin && pulsing && !harmful;

        /// <summary>
        /// Owner 2026-10-26: the armor chant (Crusader's Ward and its ranks) still
        /// showed wings and a glowing sphere on every member with no server
        /// animation, so a /spawn Paladin leaves it out of its group twist.
        /// </summary>
        public static bool SkipsInGroupTwist(bool temporaryCompanion, bool autonomous, eSpellType spellType) =>
            temporaryCompanion && !autonomous && spellType == eSpellType.SpecArmorFactorBuff;

        public static bool SkipsInGroupTwist(GameBot bot, Spell spell) =>
            bot != null && spell != null &&
            SkipsInGroupTwist(bot.IsTemporaryGroupHelper, bot.IsAutonomousWorldBot, spell.SpellType);

        public static bool Suppresses(GameLiving caster, Spell spell) =>
            caster is GameBot { CharacterClass: not null } bot && spell != null &&
            Suppresses(bot.IsTemporaryGroupHelper, bot.IsAutonomousWorldBot, (eCharacterClass)bot.CharacterClass.ID,
                spell.IsPulsing, spell.IsHarmful);
    }
}
