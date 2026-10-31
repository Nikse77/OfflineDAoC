using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>Owner 2026-11-01: twenty minutes of patrol first, and at least twenty between one objective and the next.</summary>
    [TestFixture]
    public class UT_RvrPatrolLock
    {
        private const long Twenty = AutonomousRvrPatrolLock.MinimumMilliseconds;

        [SetUp]
        public void Reset() => AutonomousRvrPatrolLock.Clear();

        [Test]
        public void ANewForceStartsWithTwentyMinutesOfPatrol()
        {
            Assert.That(AutonomousRvrPatrolLock.Holds("lock-a", 1_000), Is.True);
            Assert.That(AutonomousRvrPatrolLock.Holds("lock-a", 1_000 + Twenty - 1), Is.True);
            Assert.That(AutonomousRvrPatrolLock.Holds("lock-a", 1_000 + Twenty), Is.False);
            Assert.That(AutonomousRvrPatrolLock.Expired("lock-a", 1_000 + Twenty), Is.True);
        }

        [Test]
        public void AssemblyRestartsTheTwentyMinutes()
        {
            AutonomousRvrPatrolLock.Holds("lock-b", 1_000);
            AutonomousRvrPatrolLock.Begin("lock-b", 600_000);
            Assert.That(AutonomousRvrPatrolLock.Holds("lock-b", 600_000 + Twenty - 1), Is.True);
        }

        [Test]
        public void AnEventEndsTheLockAndTheNextPatrolStartsANewOne()
        {
            AutonomousRvrPatrolLock.Begin("lock-c", 0);
            AutonomousRvrPatrolLock.ChoiceMade("lock-c", Twenty, sharedEvent: true);
            Assert.That(AutonomousRvrPatrolLock.Expired("lock-c", Twenty + 1), Is.False);
            // The event is over: the force asks again and patrols for twenty minutes from now.
            long over = Twenty + 3_600_000;
            Assert.That(AutonomousRvrPatrolLock.Holds("lock-c", over), Is.True);
            Assert.That(AutonomousRvrPatrolLock.Holds("lock-c", over + Twenty - 1), Is.True);
            Assert.That(AutonomousRvrPatrolLock.Holds("lock-c", over + Twenty), Is.False);
        }

        [Test]
        public void APatrolChosenAgainLastsTwentyMinutesToo()
        {
            AutonomousRvrPatrolLock.Begin("lock-d", 0);
            AutonomousRvrPatrolLock.ChoiceMade("lock-d", Twenty, sharedEvent: false);
            Assert.That(AutonomousRvrPatrolLock.Holds("lock-d", Twenty + Twenty - 1), Is.True);
            Assert.That(AutonomousRvrPatrolLock.Holds("lock-d", Twenty + Twenty), Is.False);
        }
    }
}
