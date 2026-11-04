using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using DOL.Events;
using DOL.GS.Keeps;
using DOL.Logging;

namespace DOL.GS
{
    /// <summary>
    /// Keeps claimed by the guilds the gamebots found (owner 2026-11-04). A keep taken from its lord is claimed by an automatic
    /// guild of the realm that took it, at least thirty seconds after the lord fell, if a bot of that guild is there: alive,
    /// within <see cref="PresenceRadius"/> units of the keep. Of the guilds that are present and may claim it (the keep is not under
    /// attack or already claimed, the guild is under <c>guilds_claim_limit</c> keeps) one is picked at random. Nothing is claimed
    /// when nobody qualifies within <see cref="GiveUpMilliseconds"/>; the player's guild may claim it at any time before.
    /// </summary>
    public static class AutonomousKeepClaims
    {
        private static readonly Logger Log = LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>The shortest wait between the fall of the lord and the claim.</summary>
        public const long MinimumDelayMilliseconds = 30_000;

        /// <summary>After this long without a guild able to claim it, the keep is left unclaimed.</summary>
        public const long GiveUpMilliseconds = 20 * 60_000;

        /// <summary>How close to the keep a bot of the guild must be to count as present at the siege.</summary>
        public const int PresenceRadius = 4_500;

        public const long RetryMilliseconds = 15_000;

        private sealed class Pending
        {
            public AbstractGameKeep Keep;
            public eRealm Realm;
            public long TakenAt;
            public long NextTry;
        }

        private static readonly Dictionary<ushort, Pending> PendingClaims = new();
        private static readonly object Sync = new();
        private static Timer _timer;

        /// <summary>True once the keep may be claimed: thirty seconds after the lord fell and not before the next try.</summary>
        public static bool IsDue(long now, long takenAt, long nextTry) => now >= takenAt + MinimumDelayMilliseconds && now >= nextTry;

        /// <summary>True when the time to claim has run out.</summary>
        public static bool Expired(long now, long takenAt) => now - takenAt > GiveUpMilliseconds;

        /// <summary>A bot counts as present at the siege when it is alive, in the keep's region and within the radius.</summary>
        public static bool IsPresent(bool alive, bool sameRegion, double distance) => alive && sameRegion && distance <= PresenceRadius;

        /// <summary>One of the candidates at random, or null when there is none.</summary>
        public static T PickOne<T>(IReadOnlyList<T> candidates, Random random = null) where T : class =>
            candidates == null || candidates.Count == 0 ? null : candidates[(random ?? Random.Shared).Next(candidates.Count)];

        [GameServerStartedEvent]
        public static void Start(DOLEvent e, object sender, EventArgs args)
        {
            _timer?.Dispose();
            GameEventMgr.AddHandlerUnique(KeepEvent.KeepTaken, OnKeepTaken);
            _timer = new Timer(_ => NpcService.Instance.Post<int>(_ => Tick(), 0), null, 5_000, 5_000);
        }

        [GameServerStoppedEvent]
        public static void Stop(DOLEvent e, object sender, EventArgs args)
        {
            _timer?.Dispose();
            _timer = null;
            GameEventMgr.RemoveHandler(KeepEvent.KeepTaken, OnKeepTaken);
            lock (Sync)
                PendingClaims.Clear();
        }

        private static void OnKeepTaken(DOLEvent e, object sender, EventArgs args)
        {
            if (args is not KeepEventArgs { Keep: { } keep } || keep.IsPortalKeep)
                return;
            long now = GameLoop.GameLoopTime;
            lock (Sync)
                PendingClaims[keep.KeepID] = new Pending { Keep = keep, Realm = keep.Realm, TakenAt = now, NextTry = now + MinimumDelayMilliseconds };
        }

        private static void Tick()
        {
            try
            {
                long now = GameLoop.GameLoopTime;
                Pending[] due;
                lock (Sync)
                    due = PendingClaims.Values.Where(pending => IsDue(now, pending.TakenAt, pending.NextTry) || Expired(now, pending.TakenAt)).ToArray();
                foreach (Pending pending in due)
                    TryClaim(pending, now);
            }
            catch (Exception exception)
            {
                Log.Error("Keep claim check failed", exception);
            }
        }

        private static void TryClaim(Pending pending, long now)
        {
            AbstractGameKeep keep = pending.Keep;
            if (keep.Guild != null || keep.Realm != pending.Realm)
            {
                Forget(keep, "already claimed or taken again");
                return;
            }
            if (Expired(now, pending.TakenAt))
            {
                Log.Info($"BOT_GUILD_KEEP_UNCLAIMED keep=\"{keep.Name}\" realm={keep.Realm} reason=\"no guild could claim it in time\"");
                Forget(keep, null);
                return;
            }

            var present = new Dictionary<string, int>();
            foreach (GameBot bot in AutonomousBotRegistry.Snapshot())
            {
                if (bot.Realm != keep.Realm || bot.CurrentRegionID != keep.Region) continue;
                Guild guild = AutonomousBotGuilds.GuildOf(bot.DatabaseID);
                if (guild == null || !AutonomousBotGuilds.IsAutomatic(guild)) continue;
                double dx = bot.X - keep.X, dy = bot.Y - keep.Y;
                if (!IsPresent(bot.IsAlive, true, Math.Sqrt(dx * dx + dy * dy))) continue;
                present[guild.GuildID] = present.GetValueOrDefault(guild.GuildID) + 1;
            }

            Guild[] candidates = present.Keys.Select(GuildMgr.GetGuildByGuildID)
                .Where(guild => guild != null && keep.GuildClaimRefusal(guild) == null).ToArray();
            Guild chosen = PickOne(candidates);
            if (chosen == null)
            {
                lock (Sync)
                    pending.NextTry = now + RetryMilliseconds;
                return;
            }

            keep.ClaimForGuild(chosen);
            Log.Info($"BOT_GUILD_KEEP_CLAIMED guild=\"{chosen.Name}\" keep=\"{keep.Name}\" realm={keep.Realm} " +
                     $"present={present[chosen.GuildID]} candidates={candidates.Length} claimed={chosen.ClaimedKeeps.Count}");
            Forget(keep, null);
        }

        private static void Forget(AbstractGameKeep keep, string reason)
        {
            lock (Sync)
                PendingClaims.Remove(keep.KeepID);
            if (reason != null)
                Log.Info($"BOT_GUILD_KEEP_CLAIM_DROPPED keep=\"{keep.Name}\" reason=\"{reason}\"");
        }
    }
}
