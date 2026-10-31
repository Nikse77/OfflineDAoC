using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS;

/// <summary>
/// Owner 2026-10-31, step 4 of the RvR plan for the level-50 bots: what a realm's force goes to assault when it has nothing more
/// urgent (a defense, an own relic, a siege of its realm already running; those are the event layer's business).
/// First it retakes a keep of its own frontier that another realm holds. Only when it holds all its own frontier's keeps may it
/// go to conquer a keep of another realm: in one of the other two frontiers, chosen at random, the keep nearest to the
/// realm's entry (portal) keep there, and at least half an hour after the previous conquest attempt of the realm.
/// The rest is patrol. This replaces the once-a-minute roll that opened sieges at random.
/// </summary>
public static class AutonomousRvrCampaign
{
    /// <summary>Owner 2026-10-31: at least this long between one conquest attempt of a realm and the next.</summary>
    public const long ConquestGapMilliseconds = 30 * 60_000;

    private static readonly ConcurrentDictionary<eRealm, long> LastConquest = new();

    /// <summary>The frontier map of a realm: Albion 1, Midgard 100, Hibernia 200.</summary>
    public static ushort HomeRegion(eRealm realm) => RealmRaidNeutralEvents.HomeRegion(realm);

    public static bool ConquestGapElapsed(long? lastAttemptTick, long nowTick) =>
        lastAttemptTick is not { } last || nowTick - last >= ConquestGapMilliseconds;

    public static long? LastConquestTick(eRealm realm) => LastConquest.TryGetValue(realm, out long tick) ? tick : null;

    public static void RecordConquest(eRealm realm, long nowTick) => LastConquest[realm] = nowTick;

    /// <summary>
    /// The keep a force opens an assault on, or null: a retake of its own frontier first (<paramref name="conquest"/> false), else,
    /// when the realm holds all its own frontier's keeps and the gap since its last conquest attempt has passed, a keep in one of the
    /// other two frontiers, picked at random among those that have one, the nearest to the realm's entry keep (conquest true).
    /// <paramref name="assaultable"/> are the enemy keeps free to be attacked (no siege, no cooldown); <paramref name="homeLost"/> is
    /// whether another realm holds any keep of the realm's own frontier, whatever its state; <paramref name="entry"/> gives the
    /// realm's entry keep in a frontier, when known.
    /// </summary>
    public static AutonomousRvrEventLayer.LiveObjective SelectAssault(eRealm realm,
        IReadOnlyCollection<AutonomousRvrEventLayer.LiveObjective> assaultable, bool homeLost, long? lastConquestTick, long nowTick,
        Func<ushort, (int X, int Y)?> entry, Random random, out bool conquest)
    {
        conquest = false;
        ushort home = HomeRegion(realm);
        AutonomousRvrEventLayer.LiveObjective[] retake = assaultable.Where(objective => objective.RegionId == home).ToArray();
        if (retake.Length > 0)
            return retake[random.Next(retake.Length)];
        if (homeLost || !ConquestGapElapsed(lastConquestTick, nowTick))
            return null;

        ushort[] others = new ushort[] { 1, 100, 200 }.Where(region => region != home)
            .Where(region => assaultable.Any(objective => objective.RegionId == region))
            .OrderBy(_ => random.Next()).ToArray();
        if (others.Length == 0)
            return null;
        ushort target = others[0];
        AutonomousRvrEventLayer.LiveObjective[] candidates = assaultable.Where(objective => objective.RegionId == target).ToArray();
        (int X, int Y)? gate = entry(target);
        conquest = true;
        if (gate is not { } point)
            return candidates[random.Next(candidates.Length)];
        return candidates.OrderBy(objective => Distance(objective.X, objective.Y, point.X, point.Y)).ThenBy(objective => objective.Id,
            StringComparer.Ordinal).First();
    }

    private static double Distance(int x1, int y1, int x2, int y2)
    {
        double dx = x1 - x2, dy = y1 - y2;
        return dx * dx + dy * dy;
    }

    /// <summary>The realm's portal keep in a frontier, the way in of its forces; null when it cannot be read.</summary>
    public static (int X, int Y)? EntryKeep(eRealm realm, ushort region)
    {
        try
        {
            DOL.GS.Keeps.AbstractGameKeep keep = GameServer.Instance == null ? null : GameServer.KeepManager?.GetKeepsOfRegion(region)
                .FirstOrDefault(candidate => candidate.IsPortalKeep && candidate.Realm == realm);
            return keep == null ? null : (keep.X, keep.Y);
        }
        catch (Exception)
        {
            return null;   // no running server (unit tests): the choice among the keeps is random
        }
    }
}
