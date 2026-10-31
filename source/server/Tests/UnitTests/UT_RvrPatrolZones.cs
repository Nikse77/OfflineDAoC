using System.Collections.Generic;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>Owner 2026-10-31, step 3 of the RvR plan: the patrol zones are shared equally per realm, Emain first, then Odin, then Hadrian.</summary>
    [TestFixture]
    public class UT_RvrPatrolZones
    {
        [Test]
        public void TheOrderIsEmainThenOdinThenHadrian() =>
            Assert.That(AutonomousRvrRoamZones.PatrolOrder, Is.EqualTo(new ushort[] { 200, 100, 1 }));

        [Test]
        public void TheFirstForcesGoToEmainThenOdinThenHadrianAndThenAgainFromEmain()
        {
            var counts = new Dictionary<ushort, int> { [200] = 0, [100] = 0, [1] = 0 };
            var order = new List<ushort>();
            for (int i = 0; i < 7; i++)
            {
                ushort region = AutonomousRvrRoamZones.LeastUsed(AutonomousRvrRoamZones.PatrolOrder, counts);
                order.Add(region);
                counts[region]++;
            }

            Assert.That(order, Is.EqualTo(new ushort[] { 200, 100, 1, 200, 100, 1, 200 }));
        }

        [Test]
        public void AFreedZoneIsFilledBeforeAnyOther()
        {
            var counts = new Dictionary<ushort, int> { [200] = 3, [100] = 2, [1] = 3 };

            Assert.That(AutonomousRvrRoamZones.LeastUsed(AutonomousRvrRoamZones.PatrolOrder, counts), Is.EqualTo((ushort)100));
        }

        [Test]
        public void AForceKeepsItsZoneAndTheRealmsShareThemSeparately()
        {
            long now = 1_000_000;
            string a = "test-rvr-a", b = "test-rvr-b", c = "test-rvr-c", d = "test-rvr-d";
            ushort first = AutonomousRvrRoamZones.PatrolRegionFor(a, eRealm.Albion, now);
            ushort second = AutonomousRvrRoamZones.PatrolRegionFor(b, eRealm.Albion, now);
            ushort third = AutonomousRvrRoamZones.PatrolRegionFor(c, eRealm.Albion, now);

            Assert.That(new[] { first, second, third }, Is.EquivalentTo(new ushort[] { 200, 100, 1 }), "three Albion forces cover the three zones");
            Assert.That(AutonomousRvrRoamZones.PatrolRegionFor(a, eRealm.Albion, now + 60_000), Is.EqualTo(first), "a force keeps its zone");
            Assert.That(AutonomousRvrRoamZones.PatrolRegionFor(d, eRealm.Midgard, now), Is.EqualTo((ushort)200),
                "another realm starts from Emain again");
        }

        [Test]
        public void AForceKeepsItsZoneForHoursAndLeavesItOnlyWhenItsGroupEnds()
        {
            long now = 5_000_000;
            string a = "test-hold-a", b = "test-hold-b", c = "test-hold-c", d = "test-hold-d";
            ushort first = AutonomousRvrRoamZones.PatrolRegionFor(a, eRealm.Hibernia, now);
            AutonomousRvrRoamZones.PatrolRegionFor(b, eRealm.Hibernia, now);
            AutonomousRvrRoamZones.PatrolRegionFor(c, eRealm.Hibernia, now);

            // three hours later, with no request in between (a long siege), it still has its zone
            Assert.That(AutonomousRvrRoamZones.PatrolRegionFor(a, eRealm.Hibernia, now + 3 * 60 * 60_000L), Is.EqualTo(first));

            AutonomousRvrRoamZones.Release(a);
            Assert.That(AutonomousRvrRoamZones.PatrolRegionFor(d, eRealm.Hibernia, now + 3 * 60 * 60_000L), Is.EqualTo(first),
                "the zone its group left is the first to be filled again");
        }

        [Test]
        public void WithNoKeepDataEveryPointIsPatrolled() =>
            Assert.That(AutonomousRvrRoamZones.IsPatrolPoint("test-rvr-none", eRealm.Albion, 1, 1, 1000, 1000), Is.True);
    }
}
