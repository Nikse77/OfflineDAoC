using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>
    /// Owner 2026-11-01: the frontier dungeons are out of everything RvR - never a destination, never a waypoint - except
    /// for a force on relic duty.
    /// </summary>
    [TestFixture]
    public class UT_RvrDungeonExclusion
    {
        [TestCase((ushort)246)] // Dodens Gruva
        [TestCase((ushort)276)] // Marfach Caverns
        [TestCase((ushort)277)] // Hall of the Corrupt
        public void TheFrontierDungeonsAreNamedExplicitly(ushort region) =>
            Assert.That(AutonomousRvrDungeonGuard.IsFrontierDungeon(region), Is.True);

        [TestCase((ushort)1)]   // Hadrian's Wall
        [TestCase((ushort)100)] // Odin's Gate
        [TestCase((ushort)200)] // Emain Macha
        [TestCase((ushort)10)]  // Camelot reports IsDungeon and is not one of these
        [TestCase((ushort)201)] // Tir na Nog
        public void TheFrontiersAndCapitalsAreNotFrontierDungeons(ushort region) =>
            Assert.That(AutonomousRvrDungeonGuard.IsFrontierDungeon(region), Is.False);

        [Test]
        public void AnRvRPartyOffRelicDutyStaysOutAndOnRelicDutyDoesNot()
        {
            Assert.That(AutonomousRvrDungeonGuard.ShouldStayOutOfDungeons(eAutonomousObjectiveKind.RvR, false), Is.True);
            Assert.That(AutonomousRvrDungeonGuard.ShouldStayOutOfDungeons(eAutonomousObjectiveKind.RvR, true), Is.False,
                "relic duty is the one reason a frontier party enters");
            Assert.That(AutonomousRvrDungeonGuard.ShouldStayOutOfDungeons(eAutonomousObjectiveKind.GroupPve, false), Is.False,
                "a PvE party is not an RvR party");
        }

        [Test]
        public void NoBotMeansNoExclusion()
        {
            Assert.That(AutonomousRvrDungeonGuard.ExcludesRegion(null, 276), Is.False);
            Assert.That(AutonomousRvrDungeonGuard.ExcludesEdge(null, null), Is.False);
            Assert.That(AutonomousRvrDungeonGuard.ShouldAbandonGoal(null, 0), Is.False);
        }

        [Test]
        public void ThreeRefusalsInAMinuteAbandonTheGoal()
        {
            Assert.That(AutonomousRvrDungeonGuard.RefusalsBeforeAbandon, Is.EqualTo(3));
            Assert.That(AutonomousRvrDungeonGuard.RefusalWindowMilliseconds, Is.EqualTo(60_000));
        }
    }
}
