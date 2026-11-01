using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>
    /// Owner 2026-11-01: a level-50 RvR bot belongs in the frontier. The only reason a frontier group may be inside a
    /// dungeon is a relic operation; everyone else is sent home. Counting needs a loaded world, so only the rule is
    /// pinned here.
    /// </summary>
    [TestFixture]
    public class UT_RvrDungeonGuard
    {
        [Test]
        public void AnRvRBotInsideADungeonIsSentHome()
        {
            Assert.That(AutonomousRvrDungeonGuard.ShouldReturnToFrontier(
                eAutonomousObjectiveKind.RvR, false, true, 276), Is.True, "Marfach Caverns");
            Assert.That(AutonomousRvrDungeonGuard.ShouldReturnToFrontier(
                eAutonomousObjectiveKind.RvR, false, true, 277), Is.True, "Hall of the Corrupt");
        }

        [Test]
        public void AnRvRPartyTakesNoDungeonCamp()
        {
            Assert.That(AutonomousRvrDungeonGuard.ShouldStayOutOfDungeons(
                eAutonomousObjectiveKind.RvR, false), Is.True, "a frontier party fights outdoors");
            Assert.That(AutonomousRvrDungeonGuard.ShouldStayOutOfDungeons(
                eAutonomousObjectiveKind.RvR, true), Is.False, "a relic operation may walk the approach");
            Assert.That(AutonomousRvrDungeonGuard.ShouldStayOutOfDungeons(
                eAutonomousObjectiveKind.GroupPve, false), Is.False, "a PvE party still gets its dungeon");
        }

        [Test]
        public void ARelicOperationMayBeInsideAFrontierDungeon()
        {
            Assert.That(AutonomousRvrDungeonGuard.ShouldReturnToFrontier(
                eAutonomousObjectiveKind.RvR, true, true, 276), Is.False, "a relic group uses the approach");
        }

        [Test]
        public void ADungeonGroupKeepsItsObjective()
        {
            Assert.That(AutonomousRvrDungeonGuard.ShouldReturnToFrontier(
                eAutonomousObjectiveKind.GroupPve, false, true, 276), Is.False, "a PvE group is in the dungeon on purpose");
            Assert.That(AutonomousRvrDungeonGuard.ShouldReturnToFrontier(
                eAutonomousObjectiveKind.SoloPve, false, true, 249), Is.False, "Darkness Falls expedition");
        }

        [Test]
        public void TheFrontierIsWhereItBelongs()
        {
            Assert.That(AutonomousRvrDungeonGuard.IsPatrolRegion(1), Is.True, "Hadrian's Wall");
            Assert.That(AutonomousRvrDungeonGuard.IsPatrolRegion(100), Is.True, "Odin's Gate");
            Assert.That(AutonomousRvrDungeonGuard.IsPatrolRegion(200), Is.True, "Emain Macha");
            Assert.That(AutonomousRvrDungeonGuard.ShouldReturnToFrontier(
                eAutonomousObjectiveKind.RvR, false, false, 1), Is.False);
        }
    }
}
