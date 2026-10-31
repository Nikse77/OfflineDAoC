using System;

namespace DOL.GS
{
    /// <summary>One duration: only initial assembly is excluded; recovery never pauses it.</summary>
    public sealed class AutonomousGroupTaskClock
    {
        public long DurationMilliseconds { get; }
        public long? DeadlineTick { get; private set; }
        public DateTime ExpiresUtc { get; private set; }
        public long PausedRemainingMilliseconds { get; private set; }
        public bool HasStarted { get; private set; }
        public bool IsPaused => !DeadlineTick.HasValue;

        public AutonomousGroupTaskClock(eAutonomousObjectiveKind kind, Random random = null)
        {
            random ??= Random.Shared;
            // Owner 2026-10-31: an RvR group holds for two hours; the other group tasks keep their 45 to 120 minutes.
            DurationMilliseconds = kind == eAutonomousObjectiveKind.RvR
                ? 120 * 60_000L
                : random.NextInt64(45 * 60_000L, 120 * 60_000L + 1);
            PausedRemainingMilliseconds = DurationMilliseconds;
        }

        public bool Start(long nowTick, DateTime utcNow)
        {
            if (DeadlineTick.HasValue)
                return false;
            HasStarted = true;
            DeadlineTick = nowTick + PausedRemainingMilliseconds;
            ExpiresUtc = utcNow.AddMilliseconds(PausedRemainingMilliseconds);
            return true;
        }

        public long RemainingMilliseconds(long nowTick) => DeadlineTick.HasValue
            ? Math.Max(0, DeadlineTick.Value - nowTick) : PausedRemainingMilliseconds;

        public bool HasExpired(long nowTick) => DeadlineTick.HasValue && nowTick >= DeadlineTick.Value;
    }
}
