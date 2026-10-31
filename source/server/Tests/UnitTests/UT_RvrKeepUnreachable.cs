using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>Owner 2026-11-01: a force whose bot finds no route to a keep gives that keep up for a while.</summary>
    [TestFixture]
    public class UT_RvrKeepUnreachable
    {
        [Test]
        public void AForceAvoidsAnUnreachableKeepForTwentyMinutesAndOnlyThatForce()
        {
            long now = 5_000_000;
            Assert.That(AutonomousRvrEventLayer.IsAvoided("unreach-a", "rvr-keep-990", now), Is.False);
            AutonomousRvrEventLayer.ReportUnreachable("unreach-a", "rvr-keep-990", now);
            Assert.That(AutonomousRvrEventLayer.IsAvoided("unreach-a", "rvr-keep-990", now + 1), Is.True);
            Assert.That(AutonomousRvrEventLayer.IsAvoided("unreach-a", "rvr-keep-991", now + 1), Is.False);
            Assert.That(AutonomousRvrEventLayer.IsAvoided("unreach-b", "rvr-keep-990", now + 1), Is.False);
            Assert.That(AutonomousRvrEventLayer.IsAvoided("unreach-a", "rvr-keep-990",
                now + AutonomousRvrEventLayer.UnreachableAvoidMilliseconds + 1), Is.False);
        }
    }
}
