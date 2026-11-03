using System;
using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public sealed class UT_AutonomousPopulationRamp
{
    [Test]
    public void ReachesThirtyThreePercentAtFiveMinutesAndFullRosterAtFifteen()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AutonomousPopulationRamp.DesiredActiveCount(true, 10_000, 15, TimeSpan.Zero), Is.Zero);
            Assert.That(AutonomousPopulationRamp.DesiredActiveCount(true, 10_000, 15, TimeSpan.FromMinutes(5)), Is.EqualTo(3_300));
            Assert.That(AutonomousPopulationRamp.DesiredActiveCount(true, 10_000, 15, TimeSpan.FromMinutes(10)), Is.EqualTo(6_650));
            Assert.That(AutonomousPopulationRamp.DesiredActiveCount(true, 10_000, 15, TimeSpan.FromMinutes(15)), Is.EqualTo(10_000));
        });
    }

    [Test]
    public void EarlyLoginsAreIndividuallyStaggered()
    {
        TimeSpan first = AutonomousPopulationRamp.NominalLoginOffset(0, 10_000, 15);
        TimeSpan lastEarly = AutonomousPopulationRamp.NominalLoginOffset(3_299, 10_000, 15);
        TimeSpan last = AutonomousPopulationRamp.NominalLoginOffset(9_999, 10_000, 15);

        Assert.That(first, Is.GreaterThan(TimeSpan.Zero));
        Assert.That(lastEarly, Is.EqualTo(TimeSpan.FromMinutes(5)));
        Assert.That(last, Is.EqualTo(TimeSpan.FromMinutes(15)));
    }

    [Test]
    public void SpawnQueueCanKeepUpWithTenThousandBotCurve()
    {
        double peakPerSecond = Math.Max(3_300d / TimeSpan.FromMinutes(5).TotalSeconds,
            6_700d / TimeSpan.FromMinutes(10).TotalSeconds);
        Assert.That(AutonomousPopulationController.MaximumSpawnEnqueuePerPoll,
            Is.GreaterThanOrEqualTo((int)Math.Ceiling(peakPerSecond)));
    }

    [Test]
    public void FullRosterIsNeverCapped()
    {
        Assert.That(AutonomousPopulationRamp.DesiredActiveCount(true, 500, 15, TimeSpan.FromHours(2)), Is.EqualTo(500));
    }

    [TestCase(15, 2230, 15)]
    [TestCase(15, 2000, 15)]
    [TestCase(15, 10_000, 15)]
    [TestCase(15, 1000, 8)]
    [TestCase(15, 400, 4)]
    [TestCase(15, 200, 4)]
    [TestCase(15, 1, 4)]
    [TestCase(15, 0, 4)]
    [TestCase(0, 200, 0)]
    [TestCase(5, 200, 4)]
    [TestCase(3, 200, 3)]
    public void ARosterBelowTwoThousandEntersInProportionallyLessTime(int configured, int eligible, int expected) =>
        Assert.That(AutonomousPopulationRamp.ScaledRampMinutes(configured, eligible), Is.EqualTo(expected));

    [Test]
    public void DisabledPopulationLoadsNobody()
    {
        Assert.That(AutonomousPopulationRamp.DesiredActiveCount(false, 100, 15, TimeSpan.FromHours(2)), Is.Zero);
    }
}
