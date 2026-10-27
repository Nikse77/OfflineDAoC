using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>Darkness Falls: parties from level 20; a bot with no certified exit probes less often and is moved out after three minutes.</summary>
    [TestFixture]
    public class UT_DarknessFallsExitHold
    {
        [Test]
        public void ExitProbesSlowDownAfterFourEmptyOnes()
        {
            Assert.Multiple(() =>
            {
                Assert.That(AutonomousWorldBotController.NextExitProbeDelay(1), Is.EqualTo(15_000));
                Assert.That(AutonomousWorldBotController.NextExitProbeDelay(3), Is.EqualTo(15_000));
                Assert.That(AutonomousWorldBotController.NextExitProbeDelay(4), Is.EqualTo(60_000));
            });
        }

        [TestCase(19, false)]
        [TestCase(20, true)]
        public void PartiesEnterDarknessFallsFromLevelTwenty(int lowest, bool allowed)
        {
            Assert.That(AutonomousDarknessFallsPolicy.GroupMayEnter(lowest), Is.EqualTo(allowed));
        }

        [Test]
        public void HoldingThreeMinutesIsTooLong()
        {
            long since = 100_000;
            Assert.Multiple(() =>
            {
                Assert.That(AutonomousWorldBotController.ExitHoldTooLong(0, since + 600_000), Is.False, "not holding");
                Assert.That(AutonomousWorldBotController.ExitHoldTooLong(since, since + 3 * 60_000 - 1), Is.False);
                Assert.That(AutonomousWorldBotController.ExitHoldTooLong(since, since + 3 * 60_000), Is.True);
            });
        }
    }
}
