using DOL.Database;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>
    /// Owner 2026-11-01: the everlasting siege kits must cast and stay in the bag. They take the ordinary charged-item
    /// path (the one that works on a keep door) and are spared the charge bookkeeping by MaxCharges = -1, the same
    /// convention the codebase already uses for items that need no charges.
    /// </summary>
    [TestFixture]
    public class UT_EverlastingItemUse
    {
        [Test]
        public void AnEverlastingKitKeepsItsCharges()
        {
            Assert.That(GamePlayer.ItemKeepsAfterUse(new DbInventoryItem { MaxCharges = -1 }), Is.True);
        }

        [Test]
        public void AnOrdinaryKitIsStillUsedUp()
        {
            Assert.That(GamePlayer.ItemKeepsAfterUse(new DbInventoryItem { MaxCharges = 1 }), Is.False);
            Assert.That(GamePlayer.ItemKeepsAfterUse(new DbInventoryItem { MaxCharges = 0 }), Is.False);
        }

        [Test]
        public void AMissingItemIsNotProtected()
        {
            Assert.That(GamePlayer.ItemKeepsAfterUse(null), Is.False);
        }
    }
}
