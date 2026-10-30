namespace DOL.GS
{
    /// <summary>
    /// Owner 2026-10-30, two rules for the buffs a bot casts that cost concentration:
    /// a self-only dexterity buff is cast before the others (the rest then go in the usual order, as the concentration
    /// allows), and an acuity buff only goes to casters who really use it.
    /// </summary>
    public static class BotBuffRules
    {
        /// <summary>The dexterity buffs a bot casts on itself only (the group ones keep their place in the order).</summary>
        public static bool IsSelfDexterityBuff(Spell spell) =>
            spell != null && spell.Target == eSpellTarget.SELF &&
            spell.SpellType is eSpellType.DexterityBuff or eSpellType.DexterityQuicknessBuff;

        /// <summary>
        /// Acuity raises the mana stat of the classes with a spell list only (the same rule the stat calculator applies);
        /// warriors, archers, rogues and hybrids, and pets, gain nothing from it.
        /// </summary>
        public static bool UsesAcuity(ICharacterClass characterClass) =>
            characterClass?.ClassType == eClassType.ListCaster;

        public static bool UsesAcuity(GameLiving living) => living switch
        {
            GamePlayer player => UsesAcuity(player.CharacterClass),
            IGamePlayer bot => UsesAcuity(bot.CharacterClass),
            _ => false,
        };

        /// <summary>True when this buff is wasted on this target (an acuity buff for someone who does not use acuity).</summary>
        public static bool IsWastedOn(GameLiving target, Spell spell) =>
            spell?.SpellType == eSpellType.AcuityBuff && !UsesAcuity(target);
    }
}
