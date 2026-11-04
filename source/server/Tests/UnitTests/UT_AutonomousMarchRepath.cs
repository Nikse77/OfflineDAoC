using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_AutonomousMarchRepath
    {
        [Test]
        public void AFollowerThatStandsReplansAtOnce()
        {
            Assert.That(AutonomousMarchRepath.ShouldReplan(moving: false, now: 1_000, nextAllowed: 999_999), Is.True);
        }

        [Test]
        public void AFollowerThatWalksKeepsItsRouteUntilTheIntervalHasPassed()
        {
            Assert.That(AutonomousMarchRepath.ShouldReplan(moving: true, now: 5_000, nextAllowed: 9_000), Is.False);
            Assert.That(AutonomousMarchRepath.ShouldReplan(moving: true, now: 9_000, nextAllowed: 9_000), Is.True);
            Assert.That(AutonomousMarchRepath.ShouldReplan(moving: true, now: 0, nextAllowed: 0), Is.True, "the first replan is immediate");
        }

        [Test]
        public void TheWaitIsEightSecondsPlusASpreadOfLessThanTwo()
        {
            for (int id = 0; id < 5_000; id += 7)
            {
                long wait = AutonomousMarchRepath.NextDelayMilliseconds(id);
                Assert.That(wait, Is.InRange(8_000, 9_999));
            }
            Assert.That(AutonomousMarchRepath.NextDelayMilliseconds(-1), Is.InRange(8_000, 9_999), "a negative id still gives a valid wait");
        }
    }
}
