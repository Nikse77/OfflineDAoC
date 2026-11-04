namespace DOL.GS
{
    /// <summary>
    /// When a route stall is worth a trace line (owner 2026-11-04). A bot that has not moved for 12 seconds within
    /// <see cref="ArrivedRadius"/> units of its destination has arrived and waits (meeting up, regrouping, holding a post);
    /// the line is for bots that stand far from where they were going.
    /// </summary>
    public static class RouteStallTracePolicy
    {
        public const double ArrivedRadius = 600;

        public static bool ShouldTrace(double distanceToDestination) => distanceToDestination > ArrivedRadius;
    }
}
