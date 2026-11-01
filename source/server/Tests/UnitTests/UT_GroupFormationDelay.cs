using System;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>Owner 2026-10-31: no group forms in the first minutes after the server starts; owner 2026-11-01: five, not ten.</summary>
    [TestFixture]
    public class UT_GroupFormationDelay
    {
        [Test]
        public void TheDelayIsFiveMinutes() =>
            Assert.That(AutonomousBotGroupCoordinator.FormationDelayAfterStart, Is.EqualTo(TimeSpan.FromMinutes(5)));

        [TestCase(eAutonomousObjectiveKind.RvR, 0, true)]
        [TestCase(eAutonomousObjectiveKind.RvR, 299, true)]
        [TestCase(eAutonomousObjectiveKind.RvR, 300, false)]
        [TestCase(eAutonomousObjectiveKind.GroupPve, 0, false)]     // the PvE groups are not delayed
        [TestCase(eAutonomousObjectiveKind.GroupPve, 299, false)]
        public void OnlyTheRvrGroupsAreHeldBack(eAutonomousObjectiveKind kind, int seconds, bool blocked) =>
            Assert.That(AutonomousBotGroupCoordinator.FormationBlocked(kind, TimeSpan.FromSeconds(seconds)), Is.EqualTo(blocked));

        [Test]
        public void AnRvrGroupHasTwentyMinutesToGatherTheOthersFifteen()
        {
            var rvr = new AutonomousRendezvousAttendance(AutonomousRendezvousAttendance.RvrTimeoutMilliseconds);
            var other = new AutonomousRendezvousAttendance();
            rvr.Add(1, 0);
            other.Add(1, 0);

            Assert.That(rvr.Observe(1, 19 * 60_000 + 59_999, false), Is.False);
            Assert.That(rvr.Observe(1, 20 * 60_000, false), Is.True);
            Assert.That(other.Observe(1, 15 * 60_000, false), Is.True);
            Assert.That(rvr.WaitedMilliseconds(1, 5 * 60_000), Is.EqualTo(5 * 60_000));
        }

        [TestCase(0, false)]
        [TestCase(9, false)]
        [TestCase(299, false)]   // seconds
        [TestCase(300, true)]
        [TestCase(3600, true)]
        public void GroupsFormOnlyOnceFiveMinutesHavePassed(int seconds, bool expected) =>
            Assert.That(AutonomousBotGroupCoordinator.FormationDelayElapsed(TimeSpan.FromSeconds(seconds)), Is.EqualTo(expected));
    }
}
