using System.Collections.Concurrent;
using DOL.Database;

namespace DOL.GS
{
    /// <summary>
    /// Owner 2026-10-26: /spawn companions show no name over their heads
    /// unless the player turns names on with /companionnames on (saved per
    /// character). The client treats them as NPCs, so its name options for
    /// group members never applied to them. They keep their names in the group
    /// window. Autonomous gamebots and other NPCs are unchanged.
    /// Since names were hidden some companion spell effects (Cleric buffs) no
    /// longer show; the client may drop them for an NPC sent without a name.
    /// </summary>
    public static class CompanionNameplate
    {
        public const string ShowNamesKey = "CompanionNamesShown";
        private static readonly ConcurrentDictionary<string, bool> ShownByCharacter = new();

        public static bool HidesName(bool temporaryCompanion, bool autonomous) => temporaryCompanion && !autonomous;

        public static bool HidesName(bool temporaryCompanion, bool autonomous, bool viewerShowsNames) =>
            HidesName(temporaryCompanion, autonomous) && !viewerShowsNames;

        public static bool HidesName(GameNPC npc, GamePlayer viewer) =>
            npc is GameBot bot && HidesName(bot.IsTemporaryGroupHelper, bot.IsAutonomousWorldBot, ShowsNames(viewer));

        public static bool ShowsNames(GamePlayer player)
        {
            if (player?.ObjectId == null)
                return false;
            return ShownByCharacter.GetOrAdd(player.ObjectId, id =>
                DOLDB<DbCoreCharacterXCustomParam>.SelectObject(DB.Column("DOLCharactersObjectId").IsEqualTo(id)
                    .And(DB.Column("KeyName").IsEqualTo(ShowNamesKey))) != null);
        }

        public static void SetShowsNames(GamePlayer player, bool show)
        {
            if (player?.ObjectId == null || ShowsNames(player) == show)
                return;

            var saved = DOLDB<DbCoreCharacterXCustomParam>.SelectObjects(DB.Column("DOLCharactersObjectId")
                .IsEqualTo(player.ObjectId).And(DB.Column("KeyName").IsEqualTo(ShowNamesKey)));
            if (saved.Count > 0)
                GameServer.Database.DeleteObject(saved);
            if (show)
                GameServer.Database.AddObject(new DbCoreCharacterXCustomParam(player.ObjectId, ShowNamesKey, "1"));
            ShownByCharacter[player.ObjectId] = show;

            // The client keeps an NPC's name flag until the NPC is sent again.
            foreach (GameNPC npc in player.GetNPCsInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                if (npc is not GameBot bot || !HidesName(bot.IsTemporaryGroupHelper, bot.IsAutonomousWorldBot))
                    continue;
                player.Out.SendObjectRemove(npc);
                ClientService.CreateObjectForPlayer(player, npc);
            }
        }
    }
}
