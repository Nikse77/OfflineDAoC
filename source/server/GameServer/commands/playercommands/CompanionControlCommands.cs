using System.Linq;
using DOL.AI.Brain;

namespace DOL.GS.Commands
{
    /// <summary>
    /// Owner 2026-11-01: three orders for the companions summoned with /spawn.
    ///   /cpassist  they attack the target you have selected;
    ///   /cpstop    they break off and follow you for fifteen seconds, then behave normally again;
    ///   /cpbuff    they cast every buff the group is missing.
    /// These are the player's temporary companions only, never autonomous world bots.
    /// </summary>
    internal static class CompanionOrders
    {
        /// <summary>The player's /spawn companions that are in the group and have a BotBrain.</summary>
        public static GameBot[] Of(GamePlayer player) =>
            player?.Group == null
                ? []
                : player.Group.GetMembersInTheGroup().OfType<GameBot>()
                    .Where(bot => bot.Brain is BotBrain && CompanionEngagementMode.LeaderOf(bot) == player)
                    .ToArray();
    }

    [CmdAttribute("&cpassist", ePrivLevel.Player,
        "Your companions attack the target you have selected", "/cpassist")]
    public sealed class CompanionAssistOrderCommand : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            GamePlayer player = client.Player;
            if (player.TargetObject is not GameLiving target || !target.IsAlive ||
                !GameServer.ServerRules.IsAllowedToAttack(player, target, false))
            {
                DisplayMessage(client, "Select a living enemy first, then type /cpassist.");
                return;
            }

            GameBot[] companions = CompanionOrders.Of(player);
            if (companions.Length == 0)
            {
                DisplayMessage(client, "You have no companions to order. Use /spawn first.");
                return;
            }

            foreach (GameBot bot in companions)
            {
                if (bot.IsTemporaryCompanionRestLocked)
                    bot.WakeTemporaryCompanionRest();
                ((BotBrain)bot.Brain).AssistPlayerAttack(target);
            }
            player.ControlledBrain?.Attack(target);
            DisplayMessage(client, $"Companions: ASSIST. They are attacking {target.Name}.");
        }
    }

    [CmdAttribute("&cpstop", ePrivLevel.Player,
        "Your companions break off and follow you for fifteen seconds", "/cpstop")]
    public sealed class CompanionStopOrderCommand : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            GamePlayer player = client.Player;
            GameBot[] companions = CompanionOrders.Of(player);
            if (companions.Length == 0)
            {
                DisplayMessage(client, "You have no companions to order. Use /spawn first.");
                return;
            }

            // The retreat is one order for the whole party: the same fifteen seconds on every companion, and their
            // engagement check (CompanionEngagementMode.Allows) refuses every target until it runs out.
            CompanionEngagementMode.Retreat(player);
            foreach (GameBot bot in companions)
            {
                if (bot.Brain is not BotBrain brain)
                    continue;
                brain.Disengage();
                brain.Follow(player);
                bot.StopAttack();
                bot.TargetObject = null;
            }
            player.ControlledBrain?.Body?.StopAttack();
            DisplayMessage(client,
                "Companions: RETREAT. They stop fighting and follow you for 15 seconds, then resume their normal behaviour.");
        }
    }

    [CmdAttribute("&cpbuff", ePrivLevel.Player,
        "Your companions cast every buff the group is missing", "/cpbuff")]
    public sealed class CompanionBuffOrderCommand : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            GamePlayer player = client.Player;
            GameBot[] companions = CompanionOrders.Of(player);
            if (companions.Length == 0)
            {
                DisplayMessage(client, "You have no companions to order. Use /spawn first.");
                return;
            }

            int asked = 0;
            foreach (GameBot bot in companions)
            {
                if (bot.Brain is not BotBrain brain)
                    continue;
                brain.RequestCompanionBuffBatch();
                asked++;
            }
            DisplayMessage(client,
                $"Companions: BUFF. {asked} companion(s) are casting what the group is missing.");
        }
    }
}
