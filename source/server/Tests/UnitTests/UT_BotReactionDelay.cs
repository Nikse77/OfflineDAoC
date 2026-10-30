using System;
using System.Linq;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>Owner 2026-10-30: a small random reaction time before a bot's first move in a fight and its first heal.</summary>
    [TestFixture]
    public class UT_BotReactionDelay
    {
        [Test]
        public void EveryRollStaysInsideItsRange()
        {
            var random = new Random(1);
            var engage = Enumerable.Range(0, 2000).Select(_ => BotReactionDelay.Roll(BotReactionDelay.Engage, random)).ToArray();
            var heal = Enumerable.Range(0, 2000).Select(_ => BotReactionDelay.Roll(BotReactionDelay.Heal, random)).ToArray();

            Assert.That(engage.Min(), Is.GreaterThanOrEqualTo(BotReactionDelay.EngageMinimumMilliseconds));
            Assert.That(engage.Max(), Is.LessThanOrEqualTo(BotReactionDelay.EngageMaximumMilliseconds));
            Assert.That(heal.Min(), Is.GreaterThanOrEqualTo(BotReactionDelay.HealMinimumMilliseconds));
            Assert.That(heal.Max(), Is.LessThanOrEqualTo(BotReactionDelay.HealMaximumMilliseconds));

            var retarget = Enumerable.Range(0, 2000).Select(_ => BotReactionDelay.Roll(BotReactionDelay.Retarget, random)).ToArray();
            Assert.That(retarget.Min(), Is.GreaterThanOrEqualTo(BotReactionDelay.RetargetMinimumMilliseconds));
            Assert.That(retarget.Max(), Is.LessThanOrEqualTo(BotReactionDelay.RetargetMaximumMilliseconds));
        }

        [Test]
        public void RollsDifferFromBotToBot()
        {
            var random = new Random(7);
            Assert.That(Enumerable.Range(0, 50).Select(_ => BotReactionDelay.Roll(BotReactionDelay.Engage, random)).Distinct().Count(),
                Is.GreaterThan(10));
        }

        [TestCase(1000L, 1300L, true)]
        [TestCase(1299L, 1300L, true)]
        [TestCase(1300L, 1300L, false)]
        [TestCase(1500L, 1300L, false)]
        [TestCase(1000L, 0L, false)]   // nothing armed
        public void StillReactingUntilTheTimeComes(long now, long readyAt, bool expected) =>
            Assert.That(BotReactionDelay.StillReacting(now, readyAt), Is.EqualTo(expected));
    }
}
