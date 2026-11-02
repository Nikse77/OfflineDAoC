using System;
using System.Collections.Generic;
using System.Numerics;

namespace DOL.GS;

/// <summary>
/// Where an RvR bot is put back on the map when its route dies for real. Owner 2026-11-02: never its capital - the
/// nearest place its own army holds, so a bad hundred units cannot cost it the siege it was assigned to.
/// </summary>
public static class AutonomousRvrRecovery
{
    /// <summary>A hop shorter than this leaves the bot where it failed, which is what made the pocket count reset.</summary>
    public const float MinimumRelocationHop = 600f;

    public readonly record struct RecoveryPoint(ushort RegionId, Vector3 Position, string Name);

    /// <summary>
    /// The army's muster in this frontier first (the portal outpost attackers gather at), then the realm's own border
    /// keep in the same region. Both are places the realm already holds, so a bot put there can rejoin its group.
    /// </summary>
    public static RecoveryPoint[] Candidates(GameBot bot, Vector3 current)
    {
        var points = new List<RecoveryPoint>(2);
        if (AutonomousRvrStaging.TryGetSiegeMuster(bot.Realm, bot.CurrentRegionID, out var muster) &&
            muster.RegionId == bot.CurrentRegionID)
            points.Add(new(muster.RegionId, muster.Position, muster.Name));
        if (AutonomousRvrStaging.TryGetBorderKeepNear(bot.Realm, bot.CurrentRegionID,
                new Vector2(current.X, current.Y), out var keep) && keep.RegionId == bot.CurrentRegionID)
            points.Add(new(keep.RegionId, keep.Position, keep.Name));
        return points.ToArray();
    }

    /// <summary>
    /// The first candidate that is not where the bot already stands. A point under the bot cannot repair anything: the
    /// relocation would report success while the bot failed again from the same spot.
    /// </summary>
    public static bool TryPick(bool rvrDuty, Vector3 current, IReadOnlyList<RecoveryPoint> candidates,
        out RecoveryPoint chosen)
    {
        chosen = default;
        if (!rvrDuty || candidates == null)
            return false;
        for (int i = 0; i < candidates.Count; i++)
        {
            RecoveryPoint point = candidates[i];
            if (point.RegionId == 0)
                continue;
            if (Vector3.DistanceSquared(current, point.Position) <= MinimumRelocationHop * MinimumRelocationHop)
                continue;
            chosen = point;
            return true;
        }
        return false;
    }
}
