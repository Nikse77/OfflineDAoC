using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS;

/// <summary>
/// Chooses the next login by level first (owner 2026-10-31: when the server starts the level-50 bots enter the world first, then the
/// others from the highest level down), and within that level by realm load rather than database id.
/// </summary>
public static class AutonomousRealmLoginBalancer
{
    public readonly record struct Candidate(long BotId, eRealm Realm, int Level = 0);

    public static long? SelectNext(
        IEnumerable<Candidate> candidates,
        IReadOnlyDictionary<eRealm, int> activeAndPending)
    {
        Candidate[] available = candidates?
            .Where(candidate => candidate.Realm is eRealm.Albion or eRealm.Midgard or eRealm.Hibernia)
            .OrderBy(candidate => candidate.BotId)
            .ToArray() ?? [];
        if (available.Length == 0)
            return null;

        // Only the highest level still waiting is considered: all of them enter before the next level does.
        int highestLevel = available.Max(candidate => candidate.Level);
        available = available.Where(candidate => candidate.Level == highestLevel).ToArray();

        eRealm selectedRealm = available
            .Select(candidate => candidate.Realm)
            .Distinct()
            .OrderBy(realm => activeAndPending != null && activeAndPending.TryGetValue(realm, out int count) ? count : 0)
            .ThenBy(realm => (int)realm)
            .First();

        return available.First(candidate => candidate.Realm == selectedRealm).BotId;
    }
}
