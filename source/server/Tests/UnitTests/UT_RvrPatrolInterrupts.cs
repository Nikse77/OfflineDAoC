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

        [Test]
        public void APatrollingForceBreaksOffToBringAnAssaultToItsMinimumArmy()
        {
            AutonomousRvrEventLayer.StagedAssault = true;   // the muster stage of production; the fixture clears it
            // Owner 2026-11-03: the patrol may be broken to defend or to reach the minimum number of attackers of an assault.
            var lostKeep = Keep("keep-home", eRealm.Hibernia, 1);
            var opener = new AutonomousRvrEventLayer.Force("opener", eRealm.Albion, 8, 50, 2);
            const long start = 1_000;
            Assert.That(AutonomousRvrPatrolLock.Holds(opener.GroupId, start), Is.True);
            long over = start + AutonomousRvrPatrolLock.MinimumMilliseconds;
            var opened = AutonomousRvrEventLayer.ChooseOrJoin(opener, new[] { lostKeep }, over, 0);
            Assert.That(opened?.Intent, Is.EqualTo(AutonomousRvrEventLayer.Intent.AssaultKeep));

            // A second force that began its patrol only now is still locked, yet the assault has 8 of its 20.
            var patroller = new AutonomousRvrEventLayer.Force("patroller", eRealm.Albion, 8, 50, 2);
            AutonomousRvrPatrolLock.Begin(patroller.GroupId, over);
            var joined = AutonomousRvrEventLayer.ChooseOrJoin(patroller, new[] { lostKeep }, over + 10, 0);

            Assert.That(joined, Is.Not.Null, "the patrol is broken to bring the assault to its minimum");
            Assert.That(joined.Intent, Is.EqualTo(AutonomousRvrEventLayer.Intent.AssaultKeep));
            Assert.That(joined.TargetId, Is.EqualTo("keep-home"));
            Assert.That(AutonomousRvrPatrolLock.Expired(patroller.GroupId, over + 10), Is.False, "released, not expired");
        }

        [Test]
        public void APatrollingForceStaysOnPatrolOnceTheAssaultHasItsMinimumArmy()
        {
            AutonomousRvrEventLayer.StagedAssault = true;   // the muster stage of production; the fixture clears it
            var lostKeep = Keep("keep-home", eRealm.Hibernia, 1);
            var opener = new AutonomousRvrEventLayer.Force("opener", eRealm.Albion, 8, 50, 2);
            const long start = 1_000;
            Assert.That(AutonomousRvrPatrolLock.Holds(opener.GroupId, start), Is.True);
            long over = start + AutonomousRvrPatrolLock.MinimumMilliseconds;
            Assert.That(AutonomousRvrEventLayer.ChooseOrJoin(opener, new[] { lostKeep }, over, 0)?.Intent,
                Is.EqualTo(AutonomousRvrEventLayer.Intent.AssaultKeep));
            var second = new AutonomousRvrEventLayer.Force("second", eRealm.Albion, 8, 50, 2);
            AutonomousRvrPatrolLock.Begin(second.GroupId, over);
            Assert.That(AutonomousRvrEventLayer.ChooseOrJoin(second, new[] { lostKeep }, over + 10, 0)?.Intent,
                Is.EqualTo(AutonomousRvrEventLayer.Intent.AssaultKeep), "8 + 8 is still under the minimum of 20");
            var third = new AutonomousRvrEventLayer.Force("third", eRealm.Albion, 8, 50, 2);
            AutonomousRvrPatrolLock.Begin(third.GroupId, over);
            Assert.That(AutonomousRvrEventLayer.ChooseOrJoin(third, new[] { lostKeep }, over + 20, 0)?.Intent,
                Is.EqualTo(AutonomousRvrEventLayer.Intent.AssaultKeep), "8 + 8 + 8 reaches the minimum of 20");

            // The assault now has its 24: a fourth force still inside its patrol is left alone.
            var fourth = new AutonomousRvrEventLayer.Force("fourth", eRealm.Albion, 8, 50, 2);
            AutonomousRvrPatrolLock.Begin(fourth.GroupId, over);
            var plan = AutonomousRvrEventLayer.ChooseOrJoin(fourth, new[] { lostKeep }, over + 30, 0);

            Assert.That(plan?.Intent, Is.Not.EqualTo(AutonomousRvrEventLayer.Intent.AssaultKeep));
        }

        private static AutonomousRvrEventLayer.LiveObjective Point(string id, int x, int y) =>
            new(id, id, AutonomousRvrEventLayer.Intent.Roam, eRealm.None, 1000, x, y, 0, false, 0, 0, 0, 0);

        [Test]
        public void APatrolKeepsItsPointUntilItIsReachedOrFiveMinutesPass()
        {
            // Owner 2026-11-03: the point of a patrol is held until the party reaches it, five minutes pass, or the patrol is
            // interrupted; it used to be drawn again at every review, about once a minute.
            var party = new AutonomousRvrEventLayer.Force("patrol-hold", eRealm.Albion, 8, 50, 2);
            var a = Point("rvr-roam-a", 10_000, 10_000);
            var b = Point("rvr-roam-b", 90_000, 90_000);
            const long start = 1_000;

            Assert.That(AutonomousRvrEventLayer.ChooseOrJoin(party, new[] { a }, start, 0)?.TargetId, Is.EqualTo("rvr-roam-a"));
            var far = new AutonomousRvrEventLayer.PatrolPosition(1000, 50_000, 50_000);

            Assert.That(AutonomousRvrEventLayer.ChooseOrJoin(party, new[] { b }, start + 60_000, 0, far)?.TargetId,
                Is.EqualTo("rvr-roam-a"), "a minute later, not there yet: the same point");
            Assert.That(AutonomousRvrEventLayer.ChooseOrJoin(party, new[] { b }, start + 299_000, 0, far)?.TargetId,
                Is.EqualTo("rvr-roam-a"), "just under five minutes: still the same point");
            Assert.That(AutonomousRvrEventLayer.ChooseOrJoin(party, new[] { b }, start + 300_000, 0, far)?.TargetId,
                Is.EqualTo("rvr-roam-b"), "five minutes: a new point");
        }

        [Test]
        public void APatrolChoosesANewPointOnceThePartyHasReachedIt()
        {
            var party = new AutonomousRvrEventLayer.Force("patrol-reached", eRealm.Albion, 8, 50, 2);
            var a = Point("rvr-roam-a", 10_000, 10_000);
            var b = Point("rvr-roam-b", 90_000, 90_000);
            const long start = 1_000;

            Assert.That(AutonomousRvrEventLayer.ChooseOrJoin(party, new[] { a }, start, 0)?.TargetId, Is.EqualTo("rvr-roam-a"));
            var there = new AutonomousRvrEventLayer.PatrolPosition(1000, 10_400, 10_300);

            Assert.That(AutonomousRvrEventLayer.ChooseOrJoin(party, new[] { b }, start + 60_000, 0, there)?.TargetId,
                Is.EqualTo("rvr-roam-b"), "the party is at the point: it may pick another");
        }
    }
}
