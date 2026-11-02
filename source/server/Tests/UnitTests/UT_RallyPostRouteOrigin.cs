using System.Numerics;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>
    /// Owner 2026-11-02: the route to an attacker's rally post is proven from the point the party will really stand
    /// on. A party still at home arrives through the frontier teleporter, so its landing pad is the only honest origin;
    /// proving from the region it is leaving validated posts it could never walk to (eight assigned, none present).
    /// </summary>
    [TestFixture]
    public class UT_RallyPostRouteOrigin
    {
        private static readonly Vector3 Bot = new(333_229f, 419_539f, 5_336f);
        private static readonly Vector3 Landing = new(605_743f, 293_676f, 4_839f);

        [Test]
        public void APartyAlreadyInTheKeepsRegionIsProvenFromItself()
        {
            Assert.That(AutonomousRvrRally.RouteOriginFor(botRegionId: 1, Bot, keepRegionId: 1,
                Landing, landingRegionId: 1), Is.EqualTo(Bot));
        }

        [Test]
        public void APartyComingFromAnotherRegionIsProvenFromTheLandingPad()
        {
            Assert.That(AutonomousRvrRally.RouteOriginFor(botRegionId: 200, Bot, keepRegionId: 1,
                Landing, landingRegionId: 1), Is.EqualTo(Landing));
        }

        [Test]
        public void WithoutATeleporterIntoTheKeepsRegionThereIsNothingToProve()
        {
            Assert.That(AutonomousRvrRally.RouteOriginFor(botRegionId: 200, Bot, keepRegionId: 1,
                null, landingRegionId: 0), Is.Null);
        }

        [Test]
        public void ALandingPadOutsideTheKeepsRegionIsNotUsedAsOrigin()
        {
            Assert.That(AutonomousRvrRally.RouteOriginFor(botRegionId: 200, Bot, keepRegionId: 1,
                Landing, landingRegionId: 100), Is.Null);
        }
    }
}
