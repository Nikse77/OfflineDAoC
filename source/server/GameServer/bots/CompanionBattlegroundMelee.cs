using DOL.AI.Brain;

namespace DOL.GS
{
    /// <summary>
    /// Owner 2026-10-27: in a battleground a /spawn melee companion stays with the group. It fights an enemy only
    /// when it is the player's target (or the player's pet's), when it is hitting one of the group's casters or
    /// healers in melee (the player too, when a caster), or when it is fighting the companion itself; any other enemy
    /// it lets go. Casters, healers, monsters, the frontier and PvE are unchanged.
    /// </summary>
    public static class CompanionBattlegroundMelee
    {
        public const int PeelRange = 400;

        public static bool Allows(bool fightsMe, bool playersTarget, bool meleeingCaster) => fightsMe || playersTarget || meleeingCaster;

        public static bool Applies(GameBot bot) =>
            bot is { IsTemporaryGroupHelper: true, IsAutonomousWorldBot: false } &&
            BattlegroundBrackets.IsBattlegroundRegion(bot.CurrentRegionID) && !IsCasterOrHealer(bot);

        /// <summary>False when a battleground melee companion should leave <paramref name="target"/> alone.</summary>
        public static bool Allows(GameBot bot, GameLiving target)
        {
            if (target == null || !Applies(bot)) return true;
            GamePlayer leader = CompanionPvpEngagement.Leader(bot);
            if (leader == null || !CompanionPvpEngagement.Enemy(leader, target)) return true;
            return Allows(target.TargetObject == bot, CompanionPvpEngagement.Focused(bot, target), MeleeingCaster(bot, target));
        }

        private static bool MeleeingCaster(GameBot bot, GameLiving target) =>
            target.TargetObject is GameLiving victim && victim != bot && victim.IsAlive && bot.Group != null &&
            victim.Group == bot.Group && IsCasterOrHealer(victim) && target.IsWithinRadius(victim, PeelRange);

        private static bool IsCasterOrHealer(GameLiving living) => living switch
        {
            GameBot member => BotBrain.PrefersSpellRange(member.CharacterClass, grouped: true) ||
                member.CharacterClass != null && BotPartyRoles.For((eCharacterClass)member.CharacterClass.ID) == BotPartyRole.Support,
            GamePlayer player => player.CharacterClass?.ClassType == eClassType.ListCaster ||
                player.CharacterClass != null && BotPartyRoles.For((eCharacterClass)player.CharacterClass.ID) == BotPartyRole.Support,
            _ => false,
        };
    }
}
