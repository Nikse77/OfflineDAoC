using System.Numerics;

namespace DOL.GS;

public static class AutonomousRouteRecoveryPolicy
{
    // A movement callback may describe the route that was just replaced.
    // Retain intermediate/seam failures while the intent is unchanged, but
    // never charge an obsolete destination against a freshly selected route.
    public static bool AppliesToCurrentDestination(bool destinationChanged, Vector3 failed, Vector3 current) =>
        !destinationChanged || Vector3.DistanceSquared(failed, current) <= 96 * 96;

    public const int MaximumLocalAttempts = 3;
    public const float ForwardProgressRequired = 240f;
    public const long RepeatedFailureWindowMilliseconds = 10 * 60_000L;
    // A keep courtyard is up to ~1,000 units across: bots trapped inside Nottmoor
    // Faste bounced between two spots ~460 apart and never counted as repeating.
    public const float RepeatedFailureRadius = 1024f;
    public const int FailuresBeforeSafeRelocation = 3;
    public const long ImmediateRouteFailureCooldownMilliseconds = 30_000;

    // Owner 2026-11-02: an order whose destination is already underfoot needs no path. Two hundred and twenty-six
    // of 247 emergency relocations in one hour were RvR bots failing a ~200-unit move from where they stood - most
    // of them parked at a border keep or at the portal outpost - and each one threw the bot to its capital, losing
    // its group and its siege assignment. A partial path to a point this close is a hole in the navmesh, not an
    // obstacle: only the absence of a wall between the two points is required, since a wall is the one case where
    // stopping short is the honest answer.
    public const float InPlaceArrivalRadius = 256f;

    public static bool IsInPlaceArrival(Vector3 current, Vector3 destination, bool clearLineOfSight) =>
        clearLineOfSight &&
        Vector2.Distance(new(current.X, current.Y), new(destination.X, destination.Y)) <= InPlaceArrivalRadius;

    // Owner 2026-11-02: a fight is never interrupted by a relocation. The pocket step and the floor correction already
    // refused to touch a bot in combat; the ladder that ends in a teleport did not, so a bot could vanish from the
    // melee and reappear at a muster. Held bots keep fighting and the route is retried when the fight ends.
    public static bool MayRelocate(bool alive, bool inCombat, bool attacking, bool casting, bool hasAggro) =>
        alive && !inCombat && !attacking && !casting && !hasAggro;

    public static bool HasMeaningfulForwardProgress(float baselineDistance, float currentDistance) =>
        baselineDistance >= 0 && baselineDistance - currentDistance >= ForwardProgressRequired;

    public static bool ShouldAbandon(int attemptedRecoveries) =>
        attemptedRecoveries > MaximumLocalAttempts;

    public static bool ShouldRetainMovementOrder(bool isMoving, long nowTick, long retryAfterTick) =>
        isMoving || nowTick < retryAfterTick;

    // A collision-stopped actor reports IsMoving=false. Route ownership is
    // established by the controller before this policy is called, so elapsed
    // position time—not the movement flag—is the reliable stall signal.
    public static bool ShouldRecoverActiveRouteStall(long lastProgressTick, long nowTick, long threshold) =>
        lastProgressTick > 0 && nowTick - lastProgressTick >= threshold;

    public static long ReplanDelayMilliseconds(bool emptyLiveCamp, long actorKey) =>
        (emptyLiveCamp ? 2_500 : ImmediateRouteFailureCooldownMilliseconds) +
        System.Math.Abs(actorKey % 1_500);

    /// <summary>Forward progress only clears the pocket count once the bot is out of the pocket.</summary>
    public static bool HasLeftFailurePocket(ushort pocketRegion, ushort currentRegion, Vector3 pocket, Vector3 current) =>
        pocketRegion == 0 || pocketRegion != currentRegion ||
        Vector2.DistanceSquared(new(pocket.X, pocket.Y), new(current.X, current.Y)) >
        RepeatedFailureRadius * RepeatedFailureRadius;

    public static bool IsSameRepeatedFailurePocket(ushort previousRegion, ushort currentRegion,
        Vector3 previous, Vector3 current, long elapsedMilliseconds) =>
        previousRegion == currentRegion && elapsedMilliseconds >= 0 &&
        elapsedMilliseconds <= RepeatedFailureWindowMilliseconds &&
        Vector2.DistanceSquared(new(previous.X, previous.Y), new(current.X, current.Y)) <=
        RepeatedFailureRadius * RepeatedFailureRadius;

    // Detour has a bounded search budget. A useful partial corridor is not a
    // collision: continue once from its reached end, never retry a zero-progress island.
    public static bool CanContinuePartial(PathfindingStatus status, Vector3 origin, Vector3 current) =>
        status == PathfindingStatus.PartialPathFound &&
        Vector3.DistanceSquared(origin, current) >= ForwardProgressRequired * ForwardProgressRequired;
}
