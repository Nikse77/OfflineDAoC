using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>Owner 2026-10-30: a mezzed enemy that is hurt, or faces manifest odds, is finished instead of left alone.</summary>
    [TestFixture]
    public class UT_BotPvpFinishMezzed
    {
        [TestCase(100, 3, 1, true)]    // three of ours against one free enemy: manifest odds
        [TestCase(100, 4, 2, true)]    // exactly twice as many
        [TestCase(100, 3, 2, false)]   // not twice as many: leave the mezzed one
        [TestCase(100, 1, 0, false)]   // alone, nobody to fight (no free enemy anyway)
        [TestCase(100, 1, 1, false)]   // one against one
        [TestCase(100, 1, 0, false)]
        [TestCase(49, 1, 5, true)]     // under half health: finish it whatever the odds
        [TestCase(50, 2, 5, false)]    // half health is not under half
        [TestCase(10, 2, 8, true)]
        public void FinishesTheMezzedOnlyWhenHurtOrOutnumbering(int health, int allies, int free, bool expected) =>
            Assert.That(BotPvpCrowdControl.FinishMezzed(health, allies, free), Is.EqualTo(expected));
    }
}
