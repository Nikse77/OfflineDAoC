using System.Linq;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    [TestFixture]
    public class UT_AutonomousRvrCoalition
    {
        [Test]
        public void TheFirstGroupIsTheOrganizerAndLeadsTheOthersFollowInJoiningOrder()
        {
            var coalition = new AutonomousRvrCoalition();
            Assert.That(coalition.Join("a"), Is.True);
            Assert.That(coalition.Join("b"), Is.True);
            Assert.That(coalition.Join("c"), Is.True);
            Assert.That(coalition.Join("b"), Is.False);
            Assert.That(coalition.Leader, Is.EqualTo("a"));
            Assert.That(coalition.Groups, Is.EqualTo(new[] { "a", "b", "c" }));
        }

        [Test]
        public void NoMoreThanEightGroupsJoin()
        {
            var coalition = new AutonomousRvrCoalition();
            for (int i = 0; i < 8; i++) Assert.That(coalition.Join("g" + i), Is.True);
            Assert.That(coalition.Join("g8"), Is.False);
            Assert.That(coalition.Count, Is.EqualTo(8));
        }

        [Test]
        public void WhenTheOrganizerLeavesTheFirstGroupThatAggregatedTakesTheCommand()
        {
            var coalition = new AutonomousRvrCoalition();
            coalition.Join("a"); coalition.Join("b"); coalition.Join("c");
            Assert.That(coalition.Leave("a", out bool promoted), Is.True);
            Assert.That(promoted, Is.True);
            Assert.That(coalition.Leader, Is.EqualTo("b"));
        }

        [Test]
        public void ALeavingFollowerDoesNotChangeTheLeader()
        {
            var coalition = new AutonomousRvrCoalition();
            coalition.Join("a"); coalition.Join("b");
            Assert.That(coalition.Leave("b", out bool promoted), Is.True);
            Assert.That(promoted, Is.False);
            Assert.That(coalition.Leader, Is.EqualTo("a"));
            Assert.That(coalition.Leave("zzz", out _), Is.False);
        }

        [TestCase(2, 16, true)]
        [TestCase(1, 40, false)]
        [TestCase(2, 15, false)]
        [TestCase(8, 64, true)]
        public void AssaultNeedsTwoGroupsAndSixteenBots(int groups, int bots, bool viable) =>
            Assert.That(AutonomousRvrCoalition.IsViable(groups, bots), Is.EqualTo(viable));

        [Test]
        public void TheMarchIsLedByTheLeaderOfTheOrganizingGroupThenOfTheNextInOrder()
        {
            var order = new[] { "a", "b", "c" };
            var all = new (string, long, bool)[] { ("a", 1, false), ("a", 2, true), ("b", 3, true), ("c", 4, true) };
            Assert.That(AutonomousRvrCoalition.PickMarchLeader(order, all), Is.EqualTo(2));
            // the organizing group's leader is gone: the first group that aggregated leads
            Assert.That(AutonomousRvrCoalition.PickMarchLeader(order, all.Where(c => c.Item2 != 2)), Is.EqualTo(3));
            // no group leader among the candidates: the caller falls back to the main-body election
            Assert.That(AutonomousRvrCoalition.PickMarchLeader(order, new (string, long, bool)[] { ("a", 1, false) }), Is.Null);
            Assert.That(AutonomousRvrCoalition.PickMarchLeader(order, new (string, long, bool)[] { ("zzz", 9, true) }), Is.Null);
        }

        [Test]
        public void EachGroupOfUpToEightTakesItsOwnPostBlock()
        {
            var used = new System.Collections.Generic.HashSet<int>();
            int[] first = AutonomousRvrRally.BlockAlignedSlots(used, 5, 128);
            Assert.That(first, Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
            foreach (int slot in first) used.Add(slot);
            // slots 5-7 are still free, but they belong to the first group's block: the next group starts a new block
            int[] second = AutonomousRvrRally.BlockAlignedSlots(used, 8, 128);
            Assert.That(second, Is.EqualTo(Enumerable.Range(8, 8).ToArray()));
            foreach (int slot in second) used.Add(slot);
            used.Remove(2);   // a freed slot inside block 0 does not let a newcomer share the block
            Assert.That(AutonomousRvrRally.BlockAlignedSlots(used, 3, 128), Is.EqualTo(new[] { 16, 17, 18 }));
            Assert.That(AutonomousRvrRally.BlockAlignedSlots(used, 9, 128), Is.Null);
            Assert.That(AutonomousRvrRally.BlockAlignedSlots(used, 8, 16), Is.Null);
        }

        [TestCase(0, null, 0, true, "Coalition: no group yet · needs 2 more groups and 16 more bots")]
        [TestCase(1, "albion-1", 8, true, "Coalition: 1 group · leader albion-1 · needs 1 more group and 8 more bots")]
        [TestCase(2, "albion-1", 16, true, "Coalition: 2 groups · leader albion-1 · ready to march")]
        [TestCase(3, "albion-1", 24, false, "Coalition: 3 groups · leader albion-1")]
        public void TheDashboardSaysWhatAMusterStillLacks(int groups, string leader, int bots, bool mustering, string expected) =>
            Assert.That(AutonomousRvrDashboard.CoalitionLine(groups, leader, bots, mustering), Is.EqualTo(expected));

        [Test]
        public void ReconcileReportsJoinsLeavesAndPromotionsInOrder()
        {
            var coalition = new AutonomousRvrCoalition();
            var first = coalition.Reconcile(new[] { "a", "b", "c" });
            Assert.That(first.Select(change => change.Group), Is.EqualTo(new[] { "a", "b", "c" }));
            Assert.That(first.All(change => change.Kind == AutonomousRvrCoalition.Change.Joined));

            var second = coalition.Reconcile(new[] { "b", "c", "d" });
            Assert.That(second.Select(change => (change.Kind, change.Group)), Is.EqualTo(new[]
            {
                (AutonomousRvrCoalition.Change.Left, "a"),
                (AutonomousRvrCoalition.Change.Promoted, "b"),
                (AutonomousRvrCoalition.Change.Joined, "d"),
            }));
            Assert.That(coalition.Groups, Is.EqualTo(new[] { "b", "c", "d" }));
            Assert.That(coalition.Reconcile(new[] { "b", "c", "d" }), Is.Empty);
        }
    }
}
