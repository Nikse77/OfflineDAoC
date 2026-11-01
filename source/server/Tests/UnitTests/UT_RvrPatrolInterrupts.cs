using NUnit.Framework;

namespace DOL.GS.Tests
{
    /// <summary>
    /// Owner 2026-11-01: the patrol of an RvR force may be interrupted to defend one of the realm's own keeps, never to attack:
    /// an assault waits for the patrol to run out.
    /// </summary>
    [TestFixture, NonParallelizable]
    public sealed class UT_RvrPatrolInterrupts
    {
        [SetUp]
        public void Reset()
        {
            RvrEventTestState.Clear();
            AutonomousRvrEventLayer.SiegeScheduling = true;
            AutonomousRvrPatrolLock.Clear();
        }

        [TearDown]
        public void Clear()
        {
            RvrEventTestState.Clear();
            AutonomousRvrPatrolLock.Clear();
        }

        private static AutonomousRvrEventLayer.LiveObjective Keep(string id, eRealm owner, ushort region, bool underAttack = false) =>
            new(id, id, AutonomousRvrEventLayer.Intent.AssaultKeep, owner, region, 100, 100, 0, false, 0, 0, 0, 0,
                IsRelicCarrier: false, IsPortalKeep: false, UnderAttack: underAttack);

        [Test]
        public void APatrollingForceBreaksOffToDefendItsOwnKeep()
        {
            var midgardKeep = Keep("keep-midgard", eRealm.Midgard, 100, underAttack: true);
            Assert.That(AutonomousRvrEventLayer.ForceStart(midgardKeep, eRealm.Albion, 1_000, out string reason), Is.True, reason);

            var defenders = new AutonomousRvrEventLayer.Force("patrol-defenders", eRealm.Midgard, 8, 50, 2);
            const long tick = 1_000;
            Assert.That(AutonomousRvrPatrolLock.Holds(defenders.GroupId, tick), Is.True, "the force is on patrol");

            var plan = AutonomousRvrEventLayer.ChooseOrJoin(defenders, new[] { midgardKeep }, tick, 0);

            Assert.That(plan, Is.Not.Null);
            Assert.That(plan.Intent, Is.EqualTo(AutonomousRvrEventLayer.Intent.DefendEvent));
            Assert.That(plan.TargetId, Is.EqualTo("keep-midgard"));
            // The defense interrupted the patrol: the lock is released, and a fresh patrol starts when the defense is over.
            Assert.That(AutonomousRvrPatrolLock.Expired(defenders.GroupId, tick), Is.False, "released, not expired");
            Assert.That(AutonomousRvrPatrolLock.Holds(defenders.GroupId, tick + 120 * 60_000L), Is.True,
                "a new patrol starts after the defense");
        }

        [Test]
        public void APatrollingForceDoesNotBreakOffToAttack()
        {
            // A keep of Albion's own frontier held by Hibernia is a retake: it needs no conquest gap, so the patrol is the only
            // thing that can hold the force back.
            var lostKeep = Keep("keep-home", eRealm.Hibernia, 1);
            var attackers = new AutonomousRvrEventLayer.Force("patrol-attackers", eRealm.Albion, 8, 50, 2);
            const long tick = 1_000;
            Assert.That(AutonomousRvrPatrolLock.Holds(attackers.GroupId, tick), Is.True, "the force is on patrol");

            var plan = AutonomousRvrEventLayer.ChooseOrJoin(attackers, new[] { lostKeep }, tick, 0);

            Assert.That(plan?.Intent, Is.Not.EqualTo(AutonomousRvrEventLayer.Intent.AssaultKeep),
                "the patrol refuses an assault");
            Assert.That(AutonomousRvrEventLayer.Snapshot(), Is.Empty, "no siege was opened while the patrol holds");

            // The patrol over, the same force goes for the same keep.
            long over = tick + AutonomousRvrPatrolLock.MinimumMilliseconds;
            var later = AutonomousRvrEventLayer.ChooseOrJoin(attackers, new[] { lostKeep }, over, 0);

            Assert.That(later, Is.Not.Null);
            Assert.That(later.Intent, Is.EqualTo(AutonomousRvrEventLayer.Intent.AssaultKeep));
            Assert.That(later.TargetId, Is.EqualTo("keep-home"));
        }
    }
}
