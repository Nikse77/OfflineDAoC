namespace DOL.GS
{
    /// <summary>
    /// Owner 2026-10-26: a /spawn healer (Cleric, Druid, Healer) holds its
    /// position while its group fights instead of walking back into formation
    /// between heals. It follows again when the fight is over, or at once if
    /// the leader moves beyond the leash. Autonomous gamebots are unchanged.
    /// </summary>
    public static class CompanionHealerCombatHold
    {
        public const int LeashRange = 1500;

        public static bool Holds(bool temporaryCompanion, bool autonomous, bool support, bool groupInCombat,
            bool leaderWithinLeash) =>
            temporaryCompanion && !autonomous && support && groupInCombat && leaderWithinLeash;

        public static bool Holds(GameBot bot, GamePlayer leader) =>
            bot != null && leader != null &&
            Holds(bot.IsTemporaryGroupHelper, bot.IsAutonomousWorldBot, BotPartyRoles.IsSupport(bot),
                GroupInCombat(bot, leader),
                leader.CurrentRegion == bot.CurrentRegion && bot.IsWithinRadius(leader, LeashRange));

        private static bool GroupInCombat(GameBot bot, GamePlayer leader)
        {
            if (leader.InCombat || bot.InCombat)
                return true;
            if (bot.Group == null)
                return false;
            foreach (GameLiving member in bot.Group.GetMembersInTheGroup())
            {
                if (member != null && member.IsAlive && member.InCombat)
                    return true;
            }
            return false;
        }
    }
}
