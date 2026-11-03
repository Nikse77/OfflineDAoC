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
