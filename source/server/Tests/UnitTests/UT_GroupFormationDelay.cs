using System;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>Owner 2026-10-31: no group forms in the first ten minutes after the server starts.</summary>
    [TestFixture]
    public class UT_GroupFormationDelay
    {
        [Test]
        public void TheDelayIsTenMinutes() =>
            Assert.That(AutonomousBotGroupCoordinator.FormationDelayAfterStart, Is.EqualTo(TimeSpan.FromMinutes(10)));

        [TestCase(0, false)]
        [TestCase(9, false)]
        [TestCase(599, false)]   // seconds
        [TestCase(600, true)]
        [TestCase(3600, true)]
        public void GroupsFormOnlyOnceTenMinutesHavePassed(int seconds, bool expected) =>
            Assert.That(AutonomousBotGroupCoordinator.FormationDelayElapsed(TimeSpan.FromSeconds(seconds)), Is.EqualTo(expected));
    }
}
