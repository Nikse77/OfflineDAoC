using DOL.Database;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>The spells that hold a monster rather than hurt it: companions leave them alone (PlayerLedPullCoordinator).</summary>
    [TestFixture]
    public class UT_PlayerLedCrowdControl
    {
        private static Spell Make(string type, double value = 0) =>
            new(new DbSpell { Type = type, Target = "Enemy", Value = value }, 1);

        [TestCase("Mesmerize", 0)]
        [TestCase("Stun", 0)]
        [TestCase("Amnesia", 0)]
        [TestCase("SpeedDecrease", 99)]
        [TestCase("SpeedDecrease", 100)]
        [TestCase("UnbreakableSpeedDecrease", 99)]
        public void MezStunAmnesiaAndRootAreCrowdControl(string type, double value) =>
            Assert.That(PlayerLedPullCoordinator.IsCrowdControl(Make(type, value)), Is.True);

        [TestCase("SpeedDecrease", 50)]
        [TestCase("UnbreakableSpeedDecrease", 98)]
        [TestCase("DirectDamage", 0)]
        [TestCase("Lifedrain", 0)]
        public void ASnareOrADamageSpellIsNot(string type, double value) =>
            Assert.That(PlayerLedPullCoordinator.IsCrowdControl(Make(type, value)), Is.False);

        [Test]
        public void NoSpellIsNoCrowdControl() =>
            Assert.That(PlayerLedPullCoordinator.IsCrowdControl(null), Is.False);

        [Test]
        public void NobodyHoldsAnythingWhenThereIsNoPlayer() =>
            Assert.That(PlayerLedPullCoordinator.LeaderControlTarget(null), Is.Null);
    }
}
