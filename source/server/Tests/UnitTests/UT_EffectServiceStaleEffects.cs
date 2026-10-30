using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public sealed class UT_EffectServiceStaleEffects
    {
        [Test]
        public void AnEndedEffectThatIsNotRestartingIsStale() =>
            Assert.That(EffectService.IsStaleEndedEffect(ended: true, starting: false, enabling: false), Is.True);

        [TestCase(true, true, false)]
        [TestCase(true, false, true)]
        [TestCase(true, true, true)]
        public void AnEndedEffectThatIsRestartingIsKept(bool ended, bool starting, bool enabling) =>
            Assert.That(EffectService.IsStaleEndedEffect(ended, starting, enabling), Is.False);

        [TestCase(false, false, false)]
        [TestCase(false, true, false)]
        [TestCase(false, false, true)]
        public void ALiveEffectIsNeverStale(bool ended, bool starting, bool enabling) =>
            Assert.That(EffectService.IsStaleEndedEffect(ended, starting, enabling), Is.False);
    }
}
