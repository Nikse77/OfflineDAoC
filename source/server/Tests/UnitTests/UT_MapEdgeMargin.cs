using System.Numerics;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>
    /// Owner 2026-11-02: an RvR party's route keeps 600 units from the outer edge of the map. Only the arithmetic half
    /// is covered here - the rectangle clamp. The mesh re-projection inside <see cref="AutonomousMapEdgeMargin.TryKeepInside"/>
    /// needs a loaded zone and is checked in game: a party walking a frontier border must no longer hug it.
    /// </summary>
    [TestFixture]
    public class UT_MapEdgeMargin
    {
        private const float X0 = 100_000f;
        private const float Y0 = 200_000f;
        private const float X1 = 130_000f;
        private const float Y1 = 240_000f;

        [Test]
        public void APointNearTheOuterWestBorderIsMovedInsideByTheMargin()
        {
            var point = new Vector3(X0 + 500f, 220_000f, 3_000f);

            Assert.That(AutonomousMapEdgeMargin.TryClamp(X0, Y0, X1, Y1, eMapSide.West, ref point), Is.True);
            Assert.That(point.X, Is.EqualTo(X0 + 600f));
            Assert.That(point.Y, Is.EqualTo(220_000f));
            Assert.That(point.Z, Is.EqualTo(3_000f));
        }

        [Test]
        public void APointWellInsideIsLeftAlone()
        {
            var point = new Vector3(115_000f, 220_000f, 3_000f);
            var before = point;

            Assert.That(AutonomousMapEdgeMargin.TryClamp(X0, Y0, X1, Y1, eMapSide.West | eMapSide.East, ref point), Is.False);
            Assert.That(point, Is.EqualTo(before));
        }

        [Test]
        public void ASideThatIsAnInnerBorderIsNeverClamped()
        {
            var point = new Vector3(X1 - 100f, 220_000f, 3_000f);
            var before = point;

            Assert.That(AutonomousMapEdgeMargin.TryClamp(X0, Y0, X1, Y1, eMapSide.West | eMapSide.North, ref point), Is.False);
            Assert.That(point, Is.EqualTo(before));
        }

        [Test]
        public void ACornerMovesThePointOnBothAxes()
        {
            var point = new Vector3(X0 + 120f, Y1 - 80f, 3_000f);

            Assert.That(AutonomousMapEdgeMargin.TryClamp(X0, Y0, X1, Y1, eMapSide.West | eMapSide.South, ref point), Is.True);
            Assert.That(point.X, Is.EqualTo(X0 + 600f));
            Assert.That(point.Y, Is.EqualTo(Y1 - 600f));
        }

        [Test]
        public void AZoneTooNarrowForTheMarginKeepsItsPoints()
        {
            // 1,000 units wide: the two 600-unit margins would overlap, so the axis is left as the mesh gave it.
            var point = new Vector3(50_500f, 220_000f, 3_000f);
            var before = point;

            Assert.That(AutonomousMapEdgeMargin.TryClamp(50_000f, Y0, 51_000f, Y1, eMapSide.West | eMapSide.East, ref point), Is.False);
            Assert.That(point, Is.EqualTo(before));
        }

        [Test]
        public void TheMarginIsSixHundredUnits()
        {
            Assert.That(AutonomousMapEdgeMargin.Margin, Is.EqualTo(600f));
        }
    }
}
