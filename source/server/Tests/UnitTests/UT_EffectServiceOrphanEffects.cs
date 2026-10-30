using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public sealed class UT_EffectServiceOrphanEffects
    {
        private const GameObject.eObjectState Active = GameObject.eObjectState.Active;
        private const GameObject.eObjectState Deleted = GameObject.eObjectState.Deleted;

        [Test]
        public void AnEffectOnADeletedNpcEnds() =>
            Assert.That(EffectService.OutlivedItsLiving(Deleted, ownerIsNpc: true, isPulse: false, casterState: null, casterIsNpc: false), Is.True);

        [Test]
        public void AnEffectOnADeletedPlayerIsLeftToTheirOwnLogic() =>
            Assert.That(EffectService.OutlivedItsLiving(Deleted, ownerIsNpc: false, isPulse: false, casterState: null, casterIsNpc: false), Is.False);

        [Test]
        public void APulseWhoseNpcCasterWasDeletedEnds() =>
            Assert.That(EffectService.OutlivedItsLiving(Active, ownerIsNpc: false, isPulse: true, casterState: Deleted, casterIsNpc: true), Is.True);

        [Test]
        public void APulseWhoseCasterIsAPlayerOrAliveKeepsGoing()
        {
            Assert.That(EffectService.OutlivedItsLiving(Active, false, true, Deleted, casterIsNpc: false), Is.False);
            Assert.That(EffectService.OutlivedItsLiving(Active, false, true, Active, casterIsNpc: true), Is.False);
        }

        [Test]
        public void ADeletedCasterOfAnEffectThatIsNotAPulseDoesNotMatter() =>
            Assert.That(EffectService.OutlivedItsLiving(Active, false, isPulse: false, Deleted, casterIsNpc: true), Is.False);
    }
}
