namespace DOL.GS
{
    /// <summary>
    /// Owner 2026-10-26: the armor chant (Crusader's Ward and its ranks) shows
    /// wings and a glowing sphere on every group member, even with spell
    /// effects set to Self. A /spawn Paladin in a group twists its chants and
    /// would recast that one every few seconds, so it leaves it out of its
    /// group twist. Human and autonomous Paladins keep it.
    /// </summary>
    public static class CompanionChantAnimation
    {
        public static bool SkipsInGroupTwist(bool temporaryCompanion, bool autonomous, eSpellType spellType) =>
            temporaryCompanion && !autonomous && spellType == eSpellType.SpecArmorFactorBuff;

        public static bool SkipsInGroupTwist(GameBot bot, Spell spell) =>
            bot != null && spell != null &&
            SkipsInGroupTwist(bot.IsTemporaryGroupHelper, bot.IsAutonomousWorldBot, spell.SpellType);
    }
}
