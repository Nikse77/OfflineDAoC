using System.Numerics;
using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public sealed class UT_AutonomousDarknessFallsStandoff
{
    private static readonly Vector3 Spawn = new(0, 0, 0);

    // A route that walks toward the spawn along x: 3000, 2000, 1200, 800, 300, then the spawn itself.
    private static Vector3[] Route() => [new(3000, 0, 0), new(2000, 0, 0), new(1200, 0, 0), new(800, 0, 0), new(300, 0, 0), Spawn];

    [Test]
    public void StandoffDistanceIsTheAggroRangePlusTheMargin()
    {
        Assert.That(AutonomousDarknessFallsStandoff.StandoffDistance(500), Is.EqualTo(900));
        Assert.That(AutonomousDarknessFallsStandoff.StandoffDistance(200), Is.EqualTo(600));
        Assert.That(AutonomousDarknessFallsStandoff.StandoffDistance(850), Is.EqualTo(1250));
        Assert.That(AutonomousDarknessFallsStandoff.StandoffDistance(0), Is.EqualTo(900), "a monster with no aggro range uses the default");
    }

    [Test]
    public void TheRouteStopsOnTheLastPointFarEnoughWithALineOfSight()
    {
        Vector3[] cut = AutonomousDarknessFallsStandoff.Truncate(Route(), Spawn, 900, _ => true);
        Assert.That(cut, Has.Length.EqualTo(3));
        Assert.That(cut[^1], Is.EqualTo(new Vector3(1200, 0, 0)));
    }

    [Test]
    public void AShorterStandoffStopsCloser()
    {
        Vector3[] cut = AutonomousDarknessFallsStandoff.Truncate(Route(), Spawn, 600, _ => true);
        Assert.That(cut[^1], Is.EqualTo(new Vector3(800, 0, 0)));
    }

    [Test]
    public void APointWithoutALineOfSightIsSkipped()
    {
        // 1200 is far enough but cannot see the spawn: the stop falls back to the 2000 point.
        Vector3[] cut = AutonomousDarknessFallsStandoff.Truncate(Route(), Spawn, 900, point => point.X != 1200);
        Assert.That(cut[^1], Is.EqualTo(new Vector3(2000, 0, 0)));
    }

    [Test]
    public void ARouteWithNoQualifyingPointIsLeftWhole()
    {
        Vector3[] route = [new(500, 0, 0), new(300, 0, 0), Spawn];
        Assert.That(AutonomousDarknessFallsStandoff.Truncate(route, Spawn, 900, _ => true), Is.EqualTo(route));
        Assert.That(AutonomousDarknessFallsStandoff.Truncate(Route(), Spawn, 900, _ => false), Is.EqualTo(Route()),
            "no point sees the spawn: the whole route is kept");
    }

    [Test]
    public void AtLeastTwoPointsAreKept()
    {
        Vector3[] route = [new(5000, 0, 0), new(3000, 0, 0), new(100, 0, 0), Spawn];
        Vector3[] cut = AutonomousDarknessFallsStandoff.Truncate(route, Spawn, 900, _ => true);
        Assert.That(cut, Has.Length.EqualTo(2));
        Assert.That(cut[^1], Is.EqualTo(new Vector3(3000, 0, 0)));
    }

    [Test]
    public void ATooShortRouteIsLeftAlone()
    {
        Vector3[] route = [new(2000, 0, 0), Spawn];
        Assert.That(AutonomousDarknessFallsStandoff.Truncate(route, Spawn, 900, _ => true), Is.EqualTo(route));
    }
}
