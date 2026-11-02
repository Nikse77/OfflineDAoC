using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>
    /// Owner 2026-11-02: "sistema i mercanti di collane in modo che vendano le collane giuste". The medallion a party
    /// needs to change frontier is on sale where it stands. In the played world the lists carry: OFMerchant_Alb emain
    /// and odin; _Alb_Home home only; _Alb_HomeHib home and odin; _Alb_HomeMid home and emain; OFMerchant_Mid emain and
    /// hadrian; _Mid_Home home only; _Mid_HomeAlb home and emain; _Mid_HomeHib hadrian and home; OFMerchant_Hib hadrian
    /// and odin; _Hib_Home home only; _Hib_HomeAlb home and odin; _Hib_HomeMid home and hadrian.
    /// </summary>
    [TestFixture]
    public class UT_FrontierMedallionStock
    {
        [Test]
        public void AnAlbionMerchantInOdinsGateSellsTheEmainNecklace()
        {
            Assert.That(FrontierSupplyMerchantPolicy.MedallionStock(eRealm.Albion, 100), Is.EqualTo("OFMerchant_Alb_HomeMid"));
        }

        [Test]
        public void AMidgardMerchantInEmainSellsTheHadrianNecklace()
        {
            Assert.That(FrontierSupplyMerchantPolicy.MedallionStock(eRealm.Midgard, 200), Is.EqualTo("OFMerchant_Mid_HomeHib"));
        }

        [Test]
        public void AHibernianMerchantInHadriansWallSellsTheOdinNecklace()
        {
            Assert.That(FrontierSupplyMerchantPolicy.MedallionStock(eRealm.Hibernia, 1), Is.EqualTo("OFMerchant_Hib_HomeAlb"));
        }

        [Test]
        public void EveryForeignFrontierHasItsOwnStock()
        {
            Assert.That(FrontierSupplyMerchantPolicy.MedallionStock(eRealm.Albion, 200), Is.EqualTo("OFMerchant_Alb_HomeHib"));
            Assert.That(FrontierSupplyMerchantPolicy.MedallionStock(eRealm.Midgard, 1), Is.EqualTo("OFMerchant_Mid_HomeAlb"));
            Assert.That(FrontierSupplyMerchantPolicy.MedallionStock(eRealm.Hibernia, 100), Is.EqualTo("OFMerchant_Hib_HomeMid"));
        }

        [Test]
        public void AMerchantNotPlacedInARegionYetFallsBackToTheHomeNecklaceOnly()
        {
            Assert.That(FrontierSupplyMerchantPolicy.MedallionStock(eRealm.Albion, 0), Is.EqualTo("OFMerchant_Alb_Home"));
            Assert.That(FrontierSupplyMerchantPolicy.MedallionStock(eRealm.Midgard, 0), Is.EqualTo("OFMerchant_Mid_Home"));
            Assert.That(FrontierSupplyMerchantPolicy.MedallionStock(eRealm.Hibernia, 0), Is.EqualTo("OFMerchant_Hib_Home"));
        }

        [Test]
        public void NoRealmNoStock()
        {
            Assert.That(FrontierSupplyMerchantPolicy.MedallionStock(eRealm.None, 100), Is.Null);
        }
    }
}
