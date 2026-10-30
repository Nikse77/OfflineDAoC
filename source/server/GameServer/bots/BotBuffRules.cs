namespace DOL.GS
{
    /// <summary>
    /// Owner 2026-10-30, two rules for the buffs a bot casts that cost concentration:
    /// the caster's own dexterity buffs are cast on itself before the others (the rest then go in the usual order, as the concentration
    /// allows), and an acuity buff only goes to the pure casters (the mages).
    /// </summary>
    public static class BotBuffRules
    {
        /// <summary>
        /// The dexterity buffs a bot can put on itself: the self ones, and the realm and group ones (a priest's
        /// "Blessed Agility" and the Dex/Quickness line are realm-target spells that can also be cast on the caster).
        /// A pet-only dexterity buff is not one of them.
        /// </summary>
        public static bool IsOwnDexterityBuff(Spell spell) =>
            spell != null && spell.SpellType is eSpellType.DexterityBuff or eSpellType.DexterityQuicknessBuff &&
            spell.Target is eSpellTarget.SELF or eSpellTarget.REALM or eSpellTarget.GROUP;

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
