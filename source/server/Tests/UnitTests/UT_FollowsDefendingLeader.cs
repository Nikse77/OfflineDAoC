using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>A squad follower beside a leader that defends its keep is holding on purpose, not stuck (AutonomousStuckWatchdog).</summary>
    [TestFixture]
    public class UT_FollowsDefendingLeader
    {
        [TestCase("Defending keep", 10, true, true)]
        [TestCase("Defending keep", 82, true, true)]
        [TestCase("Defending keep", 400, true, true)]
        [TestCase("Defending keep", 401, true, false)]
        [TestCase("Defending keep", 50, false, false)]
        [TestCase("Roaming the battleground", 50, true, false)]
        [TestCase("Fighting", 50, true, false)]
        [TestCase(null, 50, true, false)]
        public void OnlyAFollowerBesideItsDefendingLeaderIsHolding(string leaderActivity, int distance, bool sameRegion, bool expected) =>
            Assert.That(AutonomousStuckWatchdog.FollowsDefendingLeader(leaderActivity, distance, sameRegion), Is.EqualTo(expected));
    }
}
