using System.Numerics;
using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public sealed class UT_ServiceApproachReach
{
    // Rinon Samhaoir, Grove of Domnann, and the interaction radius of the server (world_pickup_distance).
    private static readonly Vector3 Rinon = new(425242, 445781, 6458);
    private const int Radius = 256;

    [Test]
    public void TheLedgeWhereTwoMentalistsStoodIsOutOfReach()
    {
        // 220 across, 238 below: valid for the two-dimensional resolver (radius 240, height 256), 324 away in three.
        Vector3 ledge = new(425352, 445590, 6220);
        Assert.That(Vector3.Distance(ledge, Rinon), Is.GreaterThan(Radius));
        Assert.That(AutonomousZonePointApproach.WithinInteractionReach(ledge, Rinon, Radius), Is.False);
    }

    [TestCase(425242f, 445700f, 6458f, ExpectedResult = true, TestName = "Beside the merchant on his level")]
    [TestCase(425242f, 445600f, 6458f, ExpectedResult = true, TestName = "181 away on his level")]
    [TestCase(425242f, 445781f, 6300f, ExpectedResult = true, TestName = "158 below him")]
    [TestCase(425242f, 445500f, 6458f, ExpectedResult = false, TestName = "281 across")]
    public bool OnlyAPointWithinReachInThreeDimensionsIsAnApproach(float x, float y, float z) =>
        AutonomousZonePointApproach.WithinInteractionReach(new Vector3(x, y, z), Rinon, Radius);

    [Test]
    public void TheMarginKeepsTheBotInsideTheRadiusItWillTradeIn()
    {
        Assert.That(AutonomousZonePointApproach.WithinInteractionReach(new Vector3(0, 240, 0), Vector3.Zero, Radius), Is.True);
        Assert.That(AutonomousZonePointApproach.WithinInteractionReach(new Vector3(0, 241, 0), Vector3.Zero, Radius), Is.False);
    }
}
