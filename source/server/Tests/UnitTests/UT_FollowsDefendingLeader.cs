using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>A squad follower beside a leader that holds a post is holding on purpose, not stuck (AutonomousStuckWatchdog).</summary>
    [TestFixture]
    public class UT_FollowsDefendingLeader
    {
        [TestCase("Defending keep", 10, true, false, true)]
        [TestCase("Defending keep", 82, true, false, true)]
        [TestCase("Defending keep", 400, true, false, true)]
        [TestCase("Defending keep", 401, true, false, false)]
        [TestCase("Defending Dun Murdaigean", 70, true, false, true)]
        [TestCase("Siege Aimed: 33 shots / 32 confirmed hits", 69, true, false, true)]
        [TestCase("Defending keep", 50, false, false, false)]
        [TestCase("Defending keep", 50, true, true, false)]
        [TestCase("Roaming the battleground", 50, true, false, false)]
        [TestCase("Fighting Hildiengerd", 73, true, false, false)]
        [TestCase("Traveling to the battleground", 50, true, false, false)]
        [TestCase(null, 50, true, false, false)]
        public void OnlyAFollowerBesideItsHoldingLeaderIsHolding(string leaderActivity, int distance, bool sameRegion, bool leaderMoving, bool expected) =>
            Assert.That(AutonomousStuckWatchdog.FollowsHoldingLeader(leaderActivity, distance, sameRegion, leaderMoving), Is.EqualTo(expected));
    }
}
