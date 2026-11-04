namespace DOL.GS
{
    /// <summary>
    /// When the leader's fetch of a released comrade is written to the log (owner 2026-11-04): once when a fetch begins, not on
    /// every AI turn of the walk to the keep. A fetch is one that follows the last call within <see cref="ContinuationMilliseconds"/>
    /// for the same comrade.
    /// </summary>
    public static class FetchLogPolicy
    {
        /// <summary>A call this soon after the previous one, for the same comrade, continues the same fetch.</summary>
        public const long ContinuationMilliseconds = 30_000;

        public static bool ShouldLog(long lastCallTick, long lastMemberId, long now, long memberId) =>
            lastCallTick == 0 || memberId != lastMemberId || now - lastCallTick > ContinuationMilliseconds;
    }
}
