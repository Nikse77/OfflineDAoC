using System;

namespace DOL.GS;

/// <summary>
/// Computes a gradual startup target for real bot actors.  This controls only
/// how many persisted characters are loaded; it never creates progress and it
/// never deletes or resets a character. The complete non-retired roster is the
/// target -- there is deliberately no machine-performance cap.
/// </summary>
public static class AutonomousPopulationRamp
{
    public const double EarlyPopulationFraction = 0.33d;
    public const int EarlyRampMinutes = 5;

    /// <summary>
    /// The first third of the roster enters in the first third of the ramp, five minutes at the usual fifteen. A ramp shorter
    /// than fifteen minutes used to keep a five-minute early window (or the whole ramp below five), so a four-minute ramp
    /// brought a third of the roster in four minutes and then the rest at once.
    /// </summary>
    private static double EarlyMinutes(int rampMinutes) => Math.Min(EarlyRampMinutes, rampMinutes / 3d);

    public static int DesiredActiveCount(bool enabled, int rosterCount, int rampMinutes, TimeSpan sinceStartup)
    {
        if (!enabled || rosterCount <= 0)
            return 0;

        int target = rosterCount;
        if (rampMinutes <= 0 || sinceStartup.TotalMinutes >= rampMinutes)
            return target;

        double elapsed = Math.Clamp(sinceStartup.TotalMinutes, 0, rampMinutes);
        double earlyMinutes = EarlyMinutes(rampMinutes);
        int earlyTarget = Math.Max(1, (int)Math.Ceiling(target * EarlyPopulationFraction));
        int desired;
        if (elapsed <= earlyMinutes)
        {
            // Stagger the first 33%, rather than scheduling a startup burst.
            desired = (int)Math.Floor(earlyTarget * elapsed / earlyMinutes + 1e-9);
        }
        else
        {
            double remainingMinutes = Math.Max(0.1d, rampMinutes - earlyMinutes);
            desired = earlyTarget + (int)Math.Floor((target - earlyTarget) * (elapsed - earlyMinutes) / remainingMinutes + 1e-9);
        }
        return Math.Min(target, desired);
    }

    /// <summary>The roster size at which the configured ramp applies in full.</summary>
    public const int FullRampRosterCount = 2_000;

    /// <summary>The shortest ramp a restricted roster gets.</summary>
    public const int MinimumScaledRampMinutes = 4;

    /// <summary>
    /// Owner 2026-11-03: the configured ramp (15 minutes) is the time for a roster of 2,000 or more; a smaller number of
    /// bots that may log in (a login level range) enters in proportionally less time, four minutes at least (the owner's
    /// floor; a configured ramp shorter than that is kept as it is). A ramp of zero (switched off) stays zero.
    /// </summary>
    public static int ScaledRampMinutes(int configuredMinutes, int eligibleCount)
    {
        if (configuredMinutes <= 0)
            return configuredMinutes;
        if (eligibleCount >= FullRampRosterCount)
            return configuredMinutes;
        double scaled = configuredMinutes * (double)Math.Max(0, eligibleCount) / FullRampRosterCount;
        return Math.Clamp((int)Math.Ceiling(scaled), Math.Min(MinimumScaledRampMinutes, configuredMinutes), configuredMinutes);
    }

    public static TimeSpan NominalLoginOffset(int ordinal, int rosterCount, int rampMinutes)
    {
        int target = Math.Max(0, rosterCount);
        if (target == 0 || ordinal < 0 || ordinal >= target)
            return TimeSpan.MaxValue;

        double earlyMinutes = EarlyMinutes(Math.Max(0, rampMinutes));
        int earlyTarget = Math.Max(1, (int)Math.Ceiling(target * EarlyPopulationFraction));
        double minutes = ordinal < earlyTarget
            ? earlyMinutes * (ordinal + 1d) / earlyTarget
            : earlyMinutes + Math.Max(0, rampMinutes - earlyMinutes) * (ordinal - earlyTarget + 1d) / Math.Max(1, target - earlyTarget);
        return TimeSpan.FromMinutes(minutes);
    }
}
