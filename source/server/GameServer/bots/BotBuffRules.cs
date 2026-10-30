namespace DOL.GS
{
    /// <summary>
    /// Owner 2026-10-30, two rules for the buffs a bot casts that cost concentration:
    /// a self-only dexterity buff is cast before the others (the rest then go in the usual order, as the concentration
    /// allows), and an acuity buff only goes to the pure casters (the mages).
    /// </summary>
    public static class BotBuffRules
    {
        /// <summary>The dexterity buffs a bot casts on itself only (the group ones keep their place in the order).</summary>
        public static bool IsSelfDexterityBuff(Spell spell) =>
            spell != null && spell.Target == eSpellTarget.SELF &&
            spell.SpellType is eSpellType.DexterityBuff or eSpellType.DexterityQuicknessBuff;

        /// <summary>The pure casters (the mages): the only classes that get an acuity buff (owner 2026-10-30).</summary>
        public static readonly System.Collections.Generic.HashSet<eCharacterClass> PureCasters =
        [
            eCharacterClass.Wizard, eCharacterClass.Sorcerer, eCharacterClass.Cabalist, eCharacterClass.Necromancer,
            eCharacterClass.Theurgist,
            eCharacterClass.Eldritch, eCharacterClass.Enchanter, eCharacterClass.Mentalist, eCharacterClass.Animist,
            eCharacterClass.Bainshee,
            eCharacterClass.Runemaster, eCharacterClass.Spiritmaster, eCharacterClass.Bonedancer, eCharacterClass.Warlock,
        ];

        /// <summary>
        /// Acuity goes to the pure casters only: not to priests (Cleric, Druid, Shaman...), hybrids, melee classes or pets.
        /// </summary>
        public static bool UsesAcuity(ICharacterClass characterClass) =>
            characterClass != null && PureCasters.Contains((eCharacterClass)characterClass.ID);

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
