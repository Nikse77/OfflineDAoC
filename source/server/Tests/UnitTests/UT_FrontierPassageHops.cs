using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>
    /// Owner 2026-11-02: "i raduni in altre frontiere devono SEMPRE passare dai portali". A frontier merchant sells
    /// the necklaces of its realm's frontier plus the home one, so the medallion of a third frontier is nowhere on
    /// sale where the party stands and the crossing is two passages, home first. In the played world the stock is:
    /// Gwulla in Emain (200) carries home_necklace only, Gwulla in Odin (100) carries emain and hadrian, Sall Fadri
    /// in Hadrian (1) carries emain and odin.
    /// </summary>
    [TestFixture]
    public class UT_FrontierPassageHops
    {
        [Test]
        public void EachRealmHasItsOwnFrontierAsTheMedallionHome()
        {
            Assert.That(AutonomousFrontierTransport.HomeFrontier(eRealm.Albion), Is.EqualTo(1));
            Assert.That(AutonomousFrontierTransport.HomeFrontier(eRealm.Midgard), Is.EqualTo(100));
            Assert.That(AutonomousFrontierTransport.HomeFrontier(eRealm.Hibernia), Is.EqualTo(200));
            Assert.That(AutonomousFrontierTransport.HomeFrontier(eRealm.None), Is.EqualTo(0));
        }

        [Test]
        public void AMidgardPartyInEmainReachesHadriansWallThroughItsOwnFrontierFirst()
        {
            Assert.That(AutonomousFrontierTransport.NeedsHomeHop(eRealm.Midgard, 200, 1), Is.True);
        }

        [Test]
        public void ATargetThatIsTheRealmsOwnFrontierNeedsNoExtraHop()
        {
            Assert.That(AutonomousFrontierTransport.NeedsHomeHop(eRealm.Midgard, 200, 100), Is.False);
            Assert.That(AutonomousFrontierTransport.NeedsHomeHop(eRealm.Albion, 200, 1), Is.False);
            Assert.That(AutonomousFrontierTransport.NeedsHomeHop(eRealm.Hibernia, 1, 200), Is.False);
        }

        [Test]
        public void StandingInItsOwnFrontierTheMerchantOnTheSpotSellsEveryOtherFrontier()
        {
            Assert.That(AutonomousFrontierTransport.NeedsHomeHop(eRealm.Midgard, 100, 1), Is.False);
            Assert.That(AutonomousFrontierTransport.NeedsHomeHop(eRealm.Midgard, 100, 200), Is.False);
        }

        [Test]
        public void AnAlbionPartyInEmainAlsoGoesHomeFirstBecauseItsVendorSellsOnlyTheHomeNecklace()
        {
            Assert.That(AutonomousFrontierTransport.NeedsHomeHop(eRealm.Albion, 200, 100), Is.True);
        }

        [Test]
        public void TheSameRegionAndRegionsOutsideTheFrontierNeverAskForAHop()
        {
            Assert.That(AutonomousFrontierTransport.NeedsHomeHop(eRealm.Midgard, 200, 200), Is.False);
            Assert.That(AutonomousFrontierTransport.NeedsHomeHop(eRealm.Midgard, 200, 10), Is.False);
            Assert.That(AutonomousFrontierTransport.NeedsHomeHop(eRealm.Midgard, 250, 1), Is.False);
            Assert.That(AutonomousFrontierTransport.NeedsHomeHop(eRealm.None, 200, 1), Is.False);
        }
    }
}
