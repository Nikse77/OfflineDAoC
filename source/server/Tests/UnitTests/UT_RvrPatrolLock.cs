using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>Owner 2026-11-01: thirty minutes of patrol first, and at least thirty between one objective and the next.</summary>
    [TestFixture]
    public class UT_RvrPatrolLock
    {
        private const long Patrol = AutonomousRvrPatrolLock.MinimumMilliseconds;

        [SetUp]
        public void Reset() => AutonomousRvrPatrolLock.Clear();

        [Test]
        public void ThePatrolIsThirtyMinutes()
        {
            Assert.That(AutonomousRvrPatrolLock.MinimumMilliseconds, Is.EqualTo(30 * 60_000L));
        }

        [Test]
        public void ANewForceStartsWithThirtyMinutesOfPatrol()
        {
            Assert.That(AutonomousRvrPatrolLock.Holds("lock-a", 1_000), Is.True);
            Assert.That(AutonomousRvrPatrolLock.Holds("lock-a", 1_000 + Patrol - 1), Is.True);
            Assert.That(AutonomousRvrPatrolLock.Holds("lock-a", 1_000 + Patrol), Is.False);
            Assert.That(AutonomousRvrPatrolLock.Expired("lock-a", 1_000 + Patrol), Is.True);
        }

        [Test]
        public void AssemblyRestartsTheThirtyMinutes()
        {
            AutonomousRvrPatrolLock.Holds("lock-b", 1_000);
            AutonomousRvrPatrolLock.Begin("lock-b", 600_000);
            Assert.That(AutonomousRvrPatrolLock.Holds("lock-b", 600_000 + Patrol - 1), Is.True);
        }

        [Test]
        public void AnEventEndsTheLockAndTheNextPatrolStartsANewOne()
        {
            AutonomousRvrPatrolLock.Begin("lock-c", 0);
            AutonomousRvrPatrolLock.ChoiceMade("lock-c", Patrol, sharedEvent: true);
            Assert.That(AutonomousRvrPatrolLock.Expired("lock-c", Patrol + 1), Is.False);
            // The event is over: the force asks again and patrols for thirty minutes from now.
            long over = Patrol + 3_600_000;
            Assert.That(AutonomousRvrPatrolLock.Holds("lock-c", over), Is.True);
            Assert.That(AutonomousRvrPatrolLock.Holds("lock-c", over + Patrol - 1), Is.True);
            Assert.That(AutonomousRvrPatrolLock.Holds("lock-c", over + Patrol), Is.False);
        }

        [Test]
        public void APatrolChosenAgainLastsThirtyMinutesToo()
        {
            AutonomousRvrPatrolLock.Begin("lock-d", 0);
            AutonomousRvrPatrolLock.ChoiceMade("lock-d", Patrol, sharedEvent: false);
            Assert.That(AutonomousRvrPatrolLock.Holds("lock-d", Patrol + Patrol - 1), Is.True);
            Assert.That(AutonomousRvrPatrolLock.Holds("lock-d", Patrol + Patrol), Is.False);
        }
    }
}
