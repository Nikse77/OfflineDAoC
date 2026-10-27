using DOL.GS;
using DOL.GS.PacketHandler;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>Bots answer a player's emotes and fidget only after standing idle a while.</summary>
    [TestFixture]
    public class UT_BotEmotes
    {
        [TestCase(eEmote.Wave, eEmote.Wave)]
        [TestCase(eEmote.Bow, eEmote.Bow)]
        [TestCase(eEmote.Curtsey, eEmote.Bow)]
        [TestCase(eEmote.Salute, eEmote.Salute)]
        [TestCase(eEmote.Cheer, eEmote.Cheer)]
        [TestCase(eEmote.Hug, eEmote.Blush)]
        [TestCase(eEmote.Rude, eEmote.Shrug)]
        [TestCase(eEmote.Dance, eEmote.Laugh)]
        [TestCase(eEmote.Beckon, eEmote.Yes)]
        public void BotsAnswerEmotesInKind(eEmote emote, eEmote reply)
        {
            Assert.That(BotEmotes.ReplyTo(emote), Is.EqualTo(reply));
        }

        [Test]
        public void BotsLetOtherEmotesPass()
        {
            Assert.That(BotEmotes.ReplyTo(eEmote.Ponder), Is.Null);
        }

        [Test]
        public void BotsFidgetOnlyAfterStandingIdle()
        {
            long still = 10_000;
            Assert.Multiple(() =>
            {
                Assert.That(BotEmotes.ShouldFidget(still + BotEmotes.IdleAfterMilliseconds - 1, still, 0), Is.False,
                    "not idle long enough");
                Assert.That(BotEmotes.ShouldFidget(still + BotEmotes.IdleAfterMilliseconds, still, 0), Is.True,
                    "idle long enough");
                Assert.That(BotEmotes.ShouldFidget(still + BotEmotes.IdleAfterMilliseconds, still, long.MaxValue), Is.False,
                    "the last fidget was too recent");
            });
        }
    }
}
