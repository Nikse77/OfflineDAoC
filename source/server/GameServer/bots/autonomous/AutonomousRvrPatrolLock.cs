using System.Collections.Concurrent;

namespace DOL.GS;

/// <summary>
/// Owner 2026-11-01: an RvR group begins with thirty minutes of patrol; after that it may retake or conquer a keep, join a defense or
/// hunt, and when that is over it patrols again for at least thirty minutes before it changes objective once more, and so on until its
/// tenure ends. A patrol that was chosen again after the thirty minutes also lasts thirty.
/// Owner 2026-11-01: a patrol may be interrupted to defend (the realm's own keep under assault, a defense response already running), never
/// to attack: an assault or a hunt waits for the patrol to run out.
/// </summary>
public static class AutonomousRvrPatrolLock
{
    public const long MinimumMilliseconds = 30 * 60_000;
    private static readonly ConcurrentDictionary<string, long> Until = new();

    /// <summary>The force starts a patrol now (the group has just assembled, or its siege is over).</summary>
    public static void Begin(string forceId, long nowTick)
    {
        if (!string.IsNullOrEmpty(forceId)) Until[forceId] = nowTick + MinimumMilliseconds;
    }

    /// <summary>True while the force must patrol; a force never seen before starts its twenty minutes now.</summary>
    public static bool Holds(string forceId, long nowTick)
    {
        if (string.IsNullOrEmpty(forceId)) return false;
        if (!Until.TryGetValue(forceId, out long until))
        {
            Until[forceId] = nowTick + MinimumMilliseconds;
            return true;
        }
        return nowTick < until;
    }

    /// <summary>The twenty minutes are over and the force is free to choose another objective.</summary>
    public static bool Expired(string forceId, long nowTick) =>
        !string.IsNullOrEmpty(forceId) && Until.TryGetValue(forceId, out long until) && until <= nowTick;

    /// <summary>The force chose after its patrol: an event ends the lock (a new one starts when the event is over), a patrol renews it.</summary>
    public static void ChoiceMade(string forceId, long nowTick, bool sharedEvent)
    {
        if (sharedEvent) Release(forceId);
        else Begin(forceId, nowTick);
    }

    public static void Release(string forceId)
    {
        if (!string.IsNullOrEmpty(forceId)) Until.TryRemove(forceId, out _);
    }

    public static void Clear() => Until.Clear();
}
