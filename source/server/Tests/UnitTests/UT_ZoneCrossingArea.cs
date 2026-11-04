using System.Numerics;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests;

[TestFixture]
public class UT_ZoneCrossingArea
{
    [Test]
    public void ABotAtAnEdgeTakesTheGroundOfTheNearestCrossingNotOfTheNearestRoadAnchor()
    {
        // Emain Macha 211>212 (2026-11-04): the crossing at 385504 is on main ground 21922.
        var crossings = new (Vector2, float, int)[]
        {
            (new(385024, 385440), 6625f, 21922),
            (new(385024, 385504), 6699f, 21922),
            (new(385024, 385632), 6795f, 63772),
        };
        Assert.That(AutonomousZoneItinerary.NearestCrossingArea(crossings, new(384960, 385504, 6699)), Is.EqualTo(21922));
    }

    [Test]
    public void ACrossingTooFarOrAtAnotherHeightOrWithoutAreaGivesNothing()
    {
        var far = new (Vector2, float, int)[] { (new(385024, 385504), 6699f, 21922) };
        Assert.That(AutonomousZoneItinerary.NearestCrossingArea(far, new(384960, 385504 + 257, 6699)), Is.Null, "beyond 256 units");
        Assert.That(AutonomousZoneItinerary.NearestCrossingArea(far, new(384960, 385504, 6699 + 97)), Is.Null, "another floor");
        Assert.That(AutonomousZoneItinerary.NearestCrossingArea(new (Vector2, float, int)[] { (new(385024, 385504), 6699f, -1) },
            new(384960, 385504, 6699)), Is.Null, "an old file without area ids");
    }
}
