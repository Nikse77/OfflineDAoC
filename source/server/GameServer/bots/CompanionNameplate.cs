namespace DOL.GS
{
    /// <summary>
    /// Owner 2026-10-26: /spawn companions never show their name over their
    /// heads. The client treats them as NPCs, so its name options for group
    /// members never applied to them. They keep their names in the group
    /// window. Autonomous gamebots and other NPCs are unchanged.
    /// </summary>
    public static class CompanionNameplate
    {
        public static bool HidesName(bool temporaryCompanion, bool autonomous) => temporaryCompanion && !autonomous;

        public static bool HidesName(GameNPC npc) =>
            npc is GameBot bot && HidesName(bot.IsTemporaryGroupHelper, bot.IsAutonomousWorldBot);
    }
}
