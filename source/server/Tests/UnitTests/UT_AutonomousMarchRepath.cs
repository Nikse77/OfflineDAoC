using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_AutonomousMarchRepath
    {
        [Test]
        public void ARepeatedFetchOfTheSameComradeIsLoggedOnce()
        {
            // 2026-11-04: 365 lines for 8 fetches in fifty minutes, one per AI turn.
            Assert.That(FetchLogPolicy.ShouldLog(lastCallTick: 0, lastMemberId: 0, now: 5_000, memberId: 7), Is.True, "the first fetch");
            Assert.That(FetchLogPolicy.ShouldLog(5_000, 7, 7_000, 7), Is.False, "the next turn of the same walk");
            Assert.That(FetchLogPolicy.ShouldLog(7_000, 7, 37_000, 7), Is.False, "exactly the limit still continues");
            Assert.That(FetchLogPolicy.ShouldLog(7_000, 7, 37_001, 7), Is.True, "a pause longer than thirty seconds is a new fetch");
            Assert.That(FetchLogPolicy.ShouldLog(7_000, 7, 8_000, 9), Is.True, "another comrade");
        }

        [Test]
        public void ABotThatHasArrivedIsNotTracedAsStalledButAFarOneIs()
        {
            Assert.That(RouteStallTracePolicy.ShouldTrace(0), Is.False);
            Assert.That(RouteStallTracePolicy.ShouldTrace(600), Is.False, "exactly the radius counts as arrived");
            Assert.That(RouteStallTracePolicy.ShouldTrace(600.5), Is.True);
            Assert.That(RouteStallTracePolicy.ShouldTrace(62_630), Is.True, "the Emain Macha 211>212 seam stall");
        }

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
