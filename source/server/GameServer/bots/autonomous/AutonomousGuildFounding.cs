using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DOL.Logging;

namespace DOL.GS
{
    /// <summary>
    /// How the gamebots get their guilds (owner 2026-11-04). When a party of exactly eight gamebots forms and every member is
    /// of one realm, level 20 or more, and still in no guild: if an automatic guild of that realm has eight free places
    /// (<see cref="Capacity"/> is 64) all eight join the one with the fewest members, otherwise a new guild is founded with
    /// a generated name and emblem and all eight join it. A party never splits, a smaller party waits, and nobody leaves
    /// (a bot that is retired or deleted does, and an automatic guild left empty is deleted).
    /// </summary>
    public static class AutonomousGuildFounding
    {
        private static readonly Logger Log = LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>The most members an automatic guild holds: eight parties of eight.</summary>
        public const int Capacity = 64;

        /// <summary>The party size that joins a guild together.</summary>
        public const int PartySize = 8;

        /// <summary>The lowest level a gamebot needs to join a guild.</summary>
        public const int MinimumLevel = 20;

        /// <summary>One bot as the founding rule sees it.</summary>
        public readonly record struct Candidate(bool Autonomous, eRealm Realm, int Level, bool InAGuild);

        /// <summary>True when this party joins a guild: eight autonomous gamebots of one realm, all level 20+, none in a guild.</summary>
        public static bool PartyQualifies(IReadOnlyCollection<Candidate> party)
        {
            if (party == null || party.Count != PartySize) return false;
            eRealm realm = party.First().Realm;
            return party.All(bot => bot.Autonomous && bot.Realm == realm && bot.Level >= MinimumLevel && !bot.InAGuild);
        }

        /// <summary>
        /// The existing automatic guild a party joins: the one with the fewest members among those with places for the whole
        /// party, the first by id on a tie. Null when none has room (a new guild is founded).
        /// </summary>
        public static string ChooseGuild(IEnumerable<(string GuildId, int Members)> guildsOfTheRealm, int partySize = PartySize)
        {
            string chosen = null;
            int fewest = int.MaxValue;
            foreach ((string guildId, int members) in (guildsOfTheRealm ?? []).OrderBy(guild => guild.GuildId, StringComparer.Ordinal))
            {
                if (Capacity - members < partySize || members >= fewest) continue;
                chosen = guildId;
                fewest = members;
            }
            return chosen;
        }

        /// <summary>Called when an autonomous party has just formed; the work runs on the NPC thread a moment later.</summary>
        public static void OnPartyFormed(GameBot[] members)
        {
            if (members == null || members.Length != PartySize) return;
            GameBot[] party = [.. members];
            NpcService.Instance.Post<int>(_ => Place(party), 0);
        }

        private static void Place(GameBot[] party)
        {
            try
            {
                var candidates = party.Select(bot => new Candidate(bot.IsAutonomousWorldBot && !bot.IsTemporaryGroupHelper,
                    bot.Realm, bot.Level, AutonomousBotGuilds.GuildOf(bot.DatabaseID) != null)).ToArray();
                if (!PartyQualifies(candidates) || party.Any(bot => bot.DatabaseID <= 0)) return;
                eRealm realm = party[0].Realm;

                var automatic = GuildMgr.GetGuilds()
                    .Where(guild => guild.Realm == realm && AutonomousBotGuilds.IsAutomatic(guild))
                    .Select(guild => (guild, Members: AutonomousBotGuilds.MembersOf(guild).Count()))
                    .ToArray();
                string chosenId = ChooseGuild(automatic.Select(entry => (entry.guild.GuildID, entry.Members)));
                Guild target = automatic.Select(entry => entry.guild).FirstOrDefault(guild => guild.GuildID == chosenId);
                bool founded = false;
                if (target == null)
                {
                    target = Found(realm);
                    founded = target != null;
                }
                if (target == null) return;

                foreach (GameBot bot in party)
                    AutonomousBotGuilds.AddAutomaticMember(bot, target);
                Log.Info($"BOT_GUILD_PARTY_JOINED guild=\"{target.Name}\" realm={realm} founded={founded} " +
                         $"members=\"{string.Join(",", party.Select(bot => bot.Name))}\" total={AutonomousBotGuilds.MembersOf(target).Count()}");
            }
            catch (Exception exception)
            {
                Log.Error("Could not place a party of gamebots in a guild", exception);
            }
        }

        /// <summary>Founds an automatic guild of this realm with a generated name and emblem; null when that fails.</summary>
        private static Guild Found(eRealm realm)
        {
            string name = AutonomousGuildNames.Generate(realm, GuildMgr.DoesGuildExist);
            if (name == null) return null;
            Guild[] guilds = GuildMgr.GetGuilds().ToArray();
            int emblem = AutonomousGuildEmblems.Generate(realm, candidate => guilds.Any(guild => guild.Emblem == candidate));
            Guild guild = GuildMgr.CreateGuild(realm, name);
            if (guild == null) return null;
            guild.Emblem = emblem;
            guild.SaveIntoDatabase();
            if (!AutonomousBotGuilds.MarkAutomatic(guild))
            {
                GuildMgr.DeleteGuild(guild.Name);
                return null;
            }
            Log.Info($"BOT_GUILD_FOUNDED guild=\"{guild.Name}\" realm={realm} emblem={emblem}");
            return guild;
        }
    }
}
