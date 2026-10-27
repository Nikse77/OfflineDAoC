using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading;
using DOL.Logging;

namespace DOL.GS;

/// <summary>
/// Owner 2026-10-27: gamebots on a battleground tour form parties that roam the field or assault the central keep
/// together. Bots of one realm already inside their bracket's battleground group up where they stand (three to
/// eight within <see cref="GatherRadius"/>), as siege parties do. The leader runs the ordinary battleground logic
/// (AutonomousWorldBotController.ExecuteBattleground: assault, defend or roam) and the others follow it in formation
/// and fight its fights. These parties are not coordinator sessions: AutonomousBotGroupCoordinator.Pulse leaves
/// them alone. A member leaves when its tour ends, it outgrows the bracket or it is no longer in the battleground;
/// a party below two members disbands.
/// </summary>
public static class AutonomousBattlegroundSquads
{
    private static readonly Logger Log = LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);

    public const int MinimumSize = 3;
    public const int MaximumSize = 8;
    public const int GatherRadius = 2_500;
    private const long PassMilliseconds = 10_000;

    private sealed class Squad
    {
        public required string Id { get; init; }
        public required ushort RegionId { get; init; }
        public required eRealm Realm { get; init; }
        public GameBot Leader;
    }

    private static readonly ConcurrentDictionary<Group, Squad> Squads = new();
    private static long _nextPassTick;
    private static int _running;
    private static int _nextSquadNumber;

    // Owner 2026-10-27: party members do not scatter, and regroup when they have. A member lets a target go once it
    // is further than this from the leader (unless it is fighting that member)...
    public const int ChaseLeash = 1_800;
    // ...and the leader waits while more than a third of the living members are further than this from it.
    public const int RegroupRadius = 1_200;

    public static bool Owns(Group group) => group != null && Squads.ContainsKey(group);

    public static bool BeyondReach(bool targetFightsMe, bool leaderNear, int targetToLeader) =>
        !targetFightsMe && leaderNear && targetToLeader > ChaseLeash;

    /// <summary>True when a party member should let <paramref name="target"/> go: it is too far from the leader.</summary>
    public static bool BeyondSquadReach(GameBot bot, GameLiving target)
    {
        GameBot leader = LeaderOf(bot);
        if (leader == null || leader == bot || target == null || target.CurrentRegionID != bot.CurrentRegionID)
            return false;
        return BeyondReach(target.TargetObject == bot,
            leader.IsAlive && leader.CurrentRegionID == bot.CurrentRegionID, (int)leader.GetDistanceTo(target));
    }

    // Owner 2026-10-27: a real regroup. When half of the other members or more are down, out of the battleground or
    // back at the realm's portal keep (released there), the whole party falls back to the portal keep and sets out
    // again together once everyone is alive, there and rested.
    public const int PortalRadius = 1_500;

    public static bool ShouldRegroupAtPortal(int downOrBack, int otherMembers) => otherMembers > 0 && downOrBack * 2 >= otherMembers;

    public static bool NeedsPortalRegroup(GameBot leader, Vector2 portal)
    {
        if (leader?.Group == null || LeaderOf(leader) != leader) return false;
        GameBot[] others = Members(leader.Group).Where(member => member != leader).ToArray();
        int downOrBack = others.Count(member => !member.IsAlive || member.CurrentRegionID != leader.CurrentRegionID ||
            Vector2.DistanceSquared(new(member.X, member.Y), portal) <= (float)PortalRadius * PortalRadius);
        return ShouldRegroupAtPortal(downOrBack, others.Length);
    }

    /// <summary>Everyone is alive, beside the leader and fully recovered: the party can set out again.</summary>
    public static bool PortalRegroupComplete(GameBot leader) =>
        leader?.Group != null && Members(leader.Group).All(member => member.IsAlive &&
            member.CurrentRegionID == leader.CurrentRegionID && leader.IsWithinRadius(member, RegroupRadius) &&
            AutonomousRestPolicy.IsFullyRecovered(member.HealthPercent, member.ManaPercent, member.EndurancePercent, member.MaxMana > 0));

    public static bool ShouldRegroup(int membersAway, int livingMembers) => livingMembers > 0 && membersAway * 3 > livingMembers;

    /// <summary>True when the leader should wait for its scattered party.</summary>
    public static bool NeedsRegroup(GameBot leader)
    {
        if (leader?.Group == null || LeaderOf(leader) != leader) return false;
        GameBot[] others = Members(leader.Group)
            .Where(member => member != leader && member.IsAlive && member.CurrentRegionID == leader.CurrentRegionID)
            .ToArray();
        return ShouldRegroup(others.Count(member => !leader.IsWithinRadius(member, RegroupRadius)), others.Length);
    }

    /// <summary>The leader of the bot's battleground party, or null when it is not in one.</summary>
    public static GameBot LeaderOf(GameBot bot) =>
        bot?.Group != null && Squads.TryGetValue(bot.Group, out Squad squad) ? squad.Leader : null;

    /// <summary>
    /// Splits bots (by position) into parties: each one takes the nearest others within <paramref name="radius"/>, up
    /// to <paramref name="maximum"/>, and forms only with at least <paramref name="minimum"/>. The rest stay solo.
    /// </summary>
    public static List<int[]> PlanSquads(IReadOnlyList<Vector3> positions, int minimum, int maximum, float radius)
    {
        var squads = new List<int[]>();
        var free = new List<int>(Enumerable.Range(0, positions.Count));
        while (free.Count >= minimum)
        {
            int leader = free[0];
            free.RemoveAt(0);
            int[] mates = free
                .Where(index => Vector3.DistanceSquared(positions[index], positions[leader]) <= radius * radius)
                .OrderBy(index => Vector3.DistanceSquared(positions[index], positions[leader]))
                .Take(maximum - 1)
                .ToArray();
            if (mates.Length + 1 < minimum) continue;
            foreach (int mate in mates) free.Remove(mate);
            squads.Add(new[] { leader }.Concat(mates).ToArray());
        }
        return squads;
    }

    /// <summary>A bot that may stay in (or join) a party in this battleground region.</summary>
    public static bool CanServe(GameBot bot, ushort regionId, DateTime utcNow) =>
        bot != null && bot.IsAutonomousWorldBot && !bot.IsTemporaryGroupHelper && !bot.IsPlayerLedGroup &&
        bot.ObjectState == GameObject.eObjectState.Active && bot.CurrentRegionID == regionId &&
        BattlegroundBrackets.ForRegion(regionId) is { } bracket && BattlegroundBrackets.Allows(bracket, bot.Level) &&
        AutonomousObjectiveAssignments.Is(bot, eAutonomousObjectiveKind.Battleground) &&
        AutonomousObjectiveAssignments.HasActivePveAssignment(bot.PersistentRecord, utcNow);

    /// <summary>Called from the coordinator's population phase; one pass every ten seconds.</summary>
    public static void Pulse(long now)
    {
        if (now < Interlocked.Read(ref _nextPassTick) || Interlocked.Exchange(ref _running, 1) == 1) return;
        try
        {
            Interlocked.Exchange(ref _nextPassTick, now + PassMilliseconds);
            DateTime utcNow = DateTime.UtcNow;
            Maintain(utcNow);
            Form(utcNow);
        }
        catch (Exception exception)
        {
            Log.Error("Battleground party maintenance failed; it will retry on the next pass.", exception);
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }

    private static GameBot[] Members(Group group) => group.GetMembersInTheGroup().OfType<GameBot>().ToArray();

    private static void Maintain(DateTime utcNow)
    {
        foreach ((Group group, Squad squad) in Squads.ToArray())
        {
            foreach (GameBot member in Members(group).Where(member => !CanServe(member, squad.RegionId, utcNow)))
                group.RemoveMember(member);
            GameBot[] members = Members(group);
            if (members.Length < 2)
            {
                Squads.TryRemove(group, out _);
                if (members.Length > 0) group.DisbandGroup();
                Log.Info($"BG_SQUAD_DISBANDED squad={squad.Id} reason=\"fewer than two members\"");
                continue;
            }
            if (squad.Leader == null || !squad.Leader.IsAlive || squad.Leader.Group != group)
            {
                GameBot leader = ChooseLeader(members.Where(m => m.IsAlive));
                if (leader != null)
                {
                    squad.Leader = leader;
                    // The game's own group leader too: formation buff deferral and the group window read it.
                    group.MakeLeader(leader);
                }
            }
        }
    }

    /// <summary>
    /// The member that leads the assaults: not a pure healer (Cleric, Druid, Healer) when another is there, and the
    /// one nearest the others.
    /// </summary>
    private static GameBot ChooseLeader(IEnumerable<GameBot> candidates)
    {
        GameBot[] bots = candidates.ToArray();
        if (bots.Length == 0) return null;
        Vector3 center = new((float)bots.Average(m => m.X), (float)bots.Average(m => m.Y), (float)bots.Average(m => m.Z));
        return bots
            .OrderBy(m => m.CharacterClass != null &&
                          BotPartyRoles.For((eCharacterClass)m.CharacterClass.ID) == BotPartyRole.Support ? 1 : 0)
            .ThenBy(m => Vector3.DistanceSquared(new(m.X, m.Y, m.Z), center))
            .First();
    }

    private static void Form(DateTime utcNow)
    {
        var candidates = AutonomousBotRegistry.Snapshot()
            .Where(bot => bot.IsAlive && bot.Group == null && !bot.InCombat && !bot.IsAttacking &&
                          !bot.IsReturningAfterRelease && BattlegroundBrackets.IsBattlegroundRegion(bot.CurrentRegionID) &&
                          !AutonomousRealmRaid.IsReserved(bot) && CanServe(bot, bot.CurrentRegionID, utcNow))
            .GroupBy(bot => (bot.CurrentRegionID, bot.Realm));
        foreach (var side in candidates)
        {
            GameBot[] bots = side.OrderBy(_ => Random.Shared.Next()).ToArray();
            foreach (int[] plan in PlanSquads(bots.Select(b => new Vector3(b.X, b.Y, b.Z)).ToArray(),
                         MinimumSize, MaximumSize, GatherRadius))
                TryForm(side.Key.CurrentRegionID, side.Key.Realm, plan.Select(index => bots[index]).ToArray());
        }
    }

    private static void TryForm(ushort regionId, eRealm realm, GameBot[] members)
    {
        GameBot leader = ChooseLeader(members);
        var group = new Group(leader);
        var squad = new Squad
        {
            Id = $"bg-{GlobalConstants.RealmToName(realm).ToLowerInvariant()}-{Interlocked.Increment(ref _nextSquadNumber):000}",
            RegionId = regionId,
            Realm = realm,
            Leader = leader,
        };
        // Registered before anyone joins: a member's coordinator pulse on another thread must never find an
        // unowned bot party and open a session for it.
        Squads[group] = squad;
        GroupMgr.AddGroup(group);
        if (!group.AddMember(leader))
        {
            Squads.TryRemove(group, out _);
            GroupMgr.RemoveGroup(group);
            return;
        }
        foreach (GameBot member in members.Where(member => member != leader)) group.AddMember(member);
        GameBot[] joined = Members(group);
        if (joined.Length < 2)
        {
            Squads.TryRemove(group, out _);
            group.DisbandGroup();
            return;
        }
        Log.Info($"BG_SQUAD_FORMED squad={squad.Id} battleground={BattlegroundBrackets.ForRegion(regionId)?.Name} " +
                 $"realm={GlobalConstants.RealmToName(realm)} size={joined.Length} members=\"{string.Join(",", joined.Select(b => b.Name))}\"");
    }
}
