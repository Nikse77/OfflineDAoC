using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>
    /// Owner 2026-11-01: an RvR party travels as one body - the leader waits at 600 units and goes on below 300 - and it
    /// waits only for the members that actually left with it.
    /// </summary>
    [TestFixture]
    public class UT_GroupMarchCohesion
    {
        [Test]
        public void TheLeaderWaitsWhenAMemberIsSevenHundredAway()
        {
            Assert.That(AutonomousGroupPace.Decide(700, waiting: false, compact: true),
                Is.EqualTo(AutonomousGroupPace.Decision.Wait));
        }

        [Test]
        public void TheLeaderKeepsWalkingBelowSixHundred()
        {
            Assert.That(AutonomousGroupPace.Decide(599, waiting: false, compact: true),
                Is.EqualTo(AutonomousGroupPace.Decision.FullSpeed));
        }

        [Test]
        public void OnceStoppedTheLeaderWaitsUntilEveryoneIsWithinThreeHundred()
        {
            Assert.That(AutonomousGroupPace.Decide(500, waiting: true, compact: true),
                Is.EqualTo(AutonomousGroupPace.Decision.Wait), "still waiting at 500");
            Assert.That(AutonomousGroupPace.Decide(250, waiting: true, compact: true),
                Is.EqualTo(AutonomousGroupPace.Decision.FullSpeed), "goes on at 250");
        }

        [Test]
        public void TheCompactRadiiAreTheOwnersNumbers()
        {
            Assert.That(AutonomousGroupPace.CompactTrailRadius, Is.EqualTo(600));
            Assert.That(AutonomousGroupPace.CompactResumeRadius, Is.EqualTo(300));
            Assert.That(AutonomousGroupMarchRoster.GraceMilliseconds, Is.EqualTo(60_000));
        }

        [Test]
        public void WithoutARosterNobodyIsHeldBack()
        {
            Assert.That(AutonomousGroupMarchRoster.IsDeparted(null, null), Is.True);
        }
    }
}
