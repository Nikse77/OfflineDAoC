using System.Runtime.CompilerServices;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>Bots buying gear with Darkness Falls seals, and only buying with coin from merchants that sell for coin.</summary>
    [TestFixture]
    public class UT_SealMerchantPurchases
    {
        [Test]
        public void BotsOnlyBuyUpgradesFromCoinMerchants() => Assert.Multiple(() =>
        {
            Assert.That(AutonomousBotEconomy.SellsForCoin(Make<GameMerchant>()), Is.True);
            Assert.That(AutonomousBotEconomy.SellsForCoin(Make<GameDiamondSealsMerchant>()), Is.False);
            Assert.That(AutonomousBotEconomy.SellsForCoin(Make<GameSapphireSealsMerchant>()), Is.False);
            Assert.That(AutonomousBotEconomy.SellsForCoin(Make<GameEmeraldSealsMerchant>()), Is.False);
            Assert.That(AutonomousBotEconomy.SellsForCoin(Make<GameBountyMerchant>()), Is.False);
            Assert.That(AutonomousBotEconomy.SellsForCoin(null), Is.False);
        });

        [Test]
        public void SealGearIsOnlyForTheMerchantsOwnRealmAndNeedsABot() => Assert.Multiple(() =>
        {
            Assert.That(GameDarknessFallsSealsMerchant.CanTrade(eRealm.Albion, eRealm.Albion), Is.True);
            Assert.That(GameDarknessFallsSealsMerchant.CanTrade(eRealm.Albion, eRealm.Midgard), Is.False);
            Assert.That(AutonomousBotEconomy.HasSealPurchase(null), Is.False);
            Assert.That(AutonomousBotEconomy.SealCount(null, "DiamondSeal"), Is.Zero);
            Assert.That(AutonomousBotEconomy.FindSealPurchase(null, Make<GameDiamondSealsMerchant>()), Is.Null);
            // Appended after the dye merchant, so the numbers of the existing service kinds are unchanged.
            Assert.That((int)eWorldServiceKind.SealMerchant, Is.EqualTo((int)eWorldServiceKind.DyeMerchant + 1));
        });

        private static T Make<T>() where T : class => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    }
}
