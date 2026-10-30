using DOL.AI;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public sealed class UT_BrainStopSpawnReturn
    {
        [Test]
        public void AnIdleNpcAwayFromItsSpawnGoesBack() =>
            Assert.That(ABrain.ShouldReturnToSpawn(intentionalWorldMove: false, deadGameBot: false, nearSpawn: false, active: false), Is.True);

        [Test]
        public void ADeadGamebotStaysWhereItFell() =>
            Assert.That(ABrain.ShouldReturnToSpawn(intentionalWorldMove: false, deadGameBot: true, nearSpawn: false, active: false), Is.False);

        [Test]
        public void AGamebotInsideAWorldMoveIsNotSentHome() =>
            Assert.That(ABrain.ShouldReturnToSpawn(intentionalWorldMove: true, deadGameBot: false, nearSpawn: false, active: false), Is.False);

        [Test]
        public void ANpcNearItsSpawnOrStillActiveStays()
        {
            Assert.That(ABrain.ShouldReturnToSpawn(false, false, nearSpawn: true, active: false), Is.False);
            Assert.That(ABrain.ShouldReturnToSpawn(false, false, nearSpawn: false, active: true), Is.False);
        }
    }
}
