using System.Linq;
using System.Numerics;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>Battleground bots of one realm group up where they stand, three to eight at a time.</summary>
    [TestFixture]
    public class UT_AutonomousBattlegroundSquads
    {
        private static Vector3[] Line(int count, float spacing) =>
            Enumerable.Range(0, count).Select(i => new Vector3(i * spacing, 0, 0)).ToArray();

        [Test]
        public void NearbyBotsFormPartiesOfAtMostEight()
        {
            var squads = AutonomousBattlegroundSquads.PlanSquads(Line(11, 100), 3, 8, 2_500);
            Assert.Multiple(() =>
            {
                Assert.That(squads.Select(s => s.Length), Is.EqualTo(new[] { 8, 3 }));
                Assert.That(squads.SelectMany(s => s).Distinct().Count(), Is.EqualTo(11), "nobody is in two parties");
            });
        }

        [Test]
        public void TooFewNearbyBotsStaySolo()
        {
            Assert.Multiple(() =>
            {
                Assert.That(AutonomousBattlegroundSquads.PlanSquads(Line(2, 100), 3, 8, 2_500), Is.Empty, "two bots are too few");
                Assert.That(AutonomousBattlegroundSquads.PlanSquads(Line(3, 3_000), 3, 8, 2_500), Is.Empty, "three bots far apart");
            });
        }

        [Test]
        public void SeparateClustersFormSeparateParties()
        {
            Vector3[] positions = Line(3, 100).Concat(Line(4, 100).Select(p => p + new Vector3(20_000, 0, 0))).ToArray();
            var squads = AutonomousBattlegroundSquads.PlanSquads(positions, 3, 8, 2_500);
            Assert.That(squads.Select(s => s.OrderBy(i => i).ToArray()),
                Is.EqualTo(new[] { new[] { 0, 1, 2 }, new[] { 3, 4, 5, 6 } }));
        }

        [Test]
        public void MembersLetFarTargetsGoUnlessTheyAreFightingThem()
        {
            Assert.Multiple(() =>
            {
                Assert.That(AutonomousBattlegroundSquads.BeyondReach(false, true, 2_500), Is.True, "too far from the leader");
                Assert.That(AutonomousBattlegroundSquads.BeyondReach(false, true, 1_000), Is.False, "close to the leader");
                Assert.That(AutonomousBattlegroundSquads.BeyondReach(true, true, 2_500), Is.False, "it is fighting this member");
                Assert.That(AutonomousBattlegroundSquads.BeyondReach(false, false, 2_500), Is.False, "the leader is down");
            });
        }

        [TestCase(0, 5, false)]
        [TestCase(1, 5, false)]
        [TestCase(2, 5, true)]
        [TestCase(1, 2, true)]
        [TestCase(0, 0, false)]
        public void LeaderWaitsWhenMoreThanAThirdAreAway(int away, int living, bool waits) =>
            Assert.That(AutonomousBattlegroundSquads.ShouldRegroup(away, living), Is.EqualTo(waits));

        [TestCase(1, 4, false)]
        [TestCase(2, 4, true)]
        [TestCase(1, 2, true)]
        [TestCase(3, 7, false)]
        [TestCase(4, 7, true)]
        [TestCase(0, 0, false)]
        public void PartyFallsBackToThePortalWhenHalfAreDownOrBack(int downOrBack, int others, bool regroups) =>
            Assert.That(AutonomousBattlegroundSquads.ShouldRegroupAtPortal(downOrBack, others), Is.EqualTo(regroups));

        [Test]
        public void AssaultCallsComeEvery30To60MinutesAndLast30()
        {
            var random = new System.Random(7);
            long[] intervals = Enumerable.Range(0, 2_000).Select(_ => AutonomousBattlegroundWave.RollCallInterval(random)).ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(intervals.Min(), Is.GreaterThanOrEqualTo(30 * 60_000L));
                Assert.That(intervals.Max(), Is.LessThanOrEqualTo(60 * 60_000L));
                Assert.That(intervals.Average() / 60_000, Is.EqualTo(45).Within(1.5), "45 minutes on average");
                Assert.That(AutonomousBattlegroundWave.AssaultCallMilliseconds, Is.EqualTo(30 * 60_000L));
            });
        }

        [Test]
        public void GamebotsReleaseInTheBattlegroundTheyStillFit()
        {
            Assert.Multiple(() =>
            {
                Assert.That(BotBattlegroundRelease.Applies(true, 252, 22), Is.True, "Thidranki, levels 20-24");
                Assert.That(BotBattlegroundRelease.Applies(true, 252, 25), Is.False, "out-levelled: the normal release");
                Assert.That(BotBattlegroundRelease.Applies(false, 252, 22), Is.False, "companions keep their own release");
                Assert.That(BotBattlegroundRelease.Applies(true, 1, 22), Is.False, "not a battleground");
            });
        }
    }
}
