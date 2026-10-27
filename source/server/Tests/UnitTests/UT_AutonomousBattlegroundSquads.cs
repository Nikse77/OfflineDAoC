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
