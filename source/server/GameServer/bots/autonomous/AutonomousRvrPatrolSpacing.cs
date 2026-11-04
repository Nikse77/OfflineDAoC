using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS
{
    /// <summary>
    /// Owner 2026-11-04: when a patrolling party reaches the point it was heading for, or changes it, the next point is at least
    /// <see cref="MinimumDistance"/> units from the previous one, if there is such a point. If none is that far, the farthest
    /// available is taken, so a party still moves on. The previous point is the one the party held (reached or expired).
    /// </summary>
    public static class AutonomousRvrPatrolSpacing
    {
        /// <summary>How far the next patrol point should be from the previous one.</summary>
        public const double MinimumDistance = 10_000;

        /// <summary>Straight-line distance between two points; a different region counts as infinitely far.</summary>
        public static double Distance(ushort previousRegion, int previousX, int previousY, ushort region, int x, int y)
        {
            if (previousRegion != region) return double.MaxValue;
            double dx = x - previousX, dy = y - previousY;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// One of the candidates at random among those at least <see cref="MinimumDistance"/> from the previous point, otherwise the
        /// farthest one; any candidate at random when there is no previous point. Null when there is no candidate.
        /// </summary>
        public static T Choose<T>(IReadOnlyList<T> candidates, Func<T, double> distanceFromPrevious, Random random = null) where T : class
        {
            if (candidates == null || candidates.Count == 0) return null;
            random ??= Random.Shared;
            if (distanceFromPrevious == null)
                return candidates[random.Next(candidates.Count)];

            T[] far = candidates.Where(candidate => distanceFromPrevious(candidate) >= MinimumDistance).ToArray();
            if (far.Length > 0)
                return far[random.Next(far.Length)];
            return candidates.OrderByDescending(distanceFromPrevious).First();
        }
    }
}
