using System;
using System.Collections.Generic;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_AutonomousRvrPatrolSpacing
    {
        private sealed record Point(string Name, int X);

        private static readonly Point[] Line = [new("near", 3_000), new("mid", 9_999), new("far", 10_000), new("farther", 25_000)];

        private static double FromOrigin(Point point) => Math.Abs(point.X);

        [Test]
        public void ThePointsAtLeastTenThousandUnitsAwayAreTheOnesPicked()
        {
            var seen = new HashSet<string>();
            var random = new Random(4);
            for (int i = 0; i < 200; i++)
                seen.Add(AutonomousRvrPatrolSpacing.Choose<Point>(Line, FromOrigin, random).Name);
            Assert.That(seen, Is.EquivalentTo(new[] { "far", "farther" }), "exactly 10,000 counts; 9,999 does not");
        }

        [Test]
        public void WhenNoPointIsFarEnoughTheFarthestIsTaken()
        {
            Point[] close = [new("a", 100), new("b", 4_000), new("c", 2_000)];
            Assert.That(AutonomousRvrPatrolSpacing.Choose<Point>(close, FromOrigin).Name, Is.EqualTo("b"));
        }

        [Test]
        public void WithNoPreviousPointAnyPointCanBePicked()
        {
            var seen = new HashSet<string>();
            var random = new Random(9);
            for (int i = 0; i < 300; i++)
                seen.Add(AutonomousRvrPatrolSpacing.Choose<Point>(Line, null, random).Name);
            Assert.That(seen, Has.Count.EqualTo(Line.Length));
            Assert.That(AutonomousRvrPatrolSpacing.Choose<Point>([], FromOrigin), Is.Null);
            Assert.That(AutonomousRvrPatrolSpacing.Choose<Point>(null, FromOrigin), Is.Null);
        }

        [Test]
        public void ADifferentRegionIsFarAndTheSameRegionIsMeasuredInTheFlat()
        {
            Assert.That(AutonomousRvrPatrolSpacing.Distance(100, 0, 0, 200, 0, 0), Is.EqualTo(double.MaxValue));
            Assert.That(AutonomousRvrPatrolSpacing.Distance(100, 0, 0, 100, 3_000, 4_000), Is.EqualTo(5_000).Within(0.001));
        }
    }
}
