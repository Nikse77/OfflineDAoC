using System.Numerics;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>
    /// Owner 2026-11-02: an order whose destination is already underfoot is satisfied by standing there, so it cannot
    /// become the first of three terminal route failures. 226 of 247 emergency relocations in one hour were RvR bots
    /// failing a ~200-unit move from the spot they stood on, and each one cost the bot its group and its siege.
    /// </summary>
    [TestFixture]
    public class UT_RouteInPlaceArrival
    {
        private static Vector3 Where(float x, float y) => new(x, y, 3_000f);

        [Test]
        public void TwoHundredUnitsWithNothingBetweenThemCountAsArrival()
        {
            Assert.That(AutonomousRouteRecoveryPolicy.IsInPlaceArrival(
                Where(605_894f, 292_902f), Where(605_797f, 293_058f), clearLineOfSight: true), Is.True);
        }

        [Test]
        public void TheRadiusIsTheAcceptedDistance()
        {
            float radius = AutonomousRouteRecoveryPolicy.InPlaceArrivalRadius;

            Assert.That(AutonomousRouteRecoveryPolicy.IsInPlaceArrival(
                Where(0f, 0f), Where(radius, 0f), clearLineOfSight: true), Is.True);
            Assert.That(AutonomousRouteRecoveryPolicy.IsInPlaceArrival(
                Where(0f, 0f), Where(radius + 1f, 0f), clearLineOfSight: true), Is.False);
        }

        [Test]
        public void AWallBetweenTheTwoPointsIsNeverAnArrival()
        {
            Assert.That(AutonomousRouteRecoveryPolicy.IsInPlaceArrival(
                Where(0f, 0f), Where(100f, 0f), clearLineOfSight: false), Is.False);
        }
    }
}
