using System;

namespace DOL.GS.Commands
{
    [CmdAttribute("&companionnames", ePrivLevel.Player, "Show or hide the names over your /spawn companions' heads",
        "/companionnames", "/companionnames on", "/companionnames off")]
    public sealed class CompanionNamesCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            GamePlayer player = client?.Player;
            if (player == null)
                return;

            if (args.Length == 2 && args[1].Equals("on", StringComparison.OrdinalIgnoreCase))
                CompanionNameplate.SetShowsNames(player, true);
            else if (args.Length == 2 && args[1].Equals("off", StringComparison.OrdinalIgnoreCase))
                CompanionNameplate.SetShowsNames(player, false);
            else if (args.Length != 1)
            {
                DisplayMessage(client, "Use /companionnames, /companionnames on or /companionnames off.");
                return;
            }

            DisplayMessage(client, CompanionNameplate.ShowsNames(player)
                ? "Companion names are shown over their heads. /companionnames off hides them."
                : "Companion names are hidden over their heads. /companionnames on shows them.");
        }
    }
}
