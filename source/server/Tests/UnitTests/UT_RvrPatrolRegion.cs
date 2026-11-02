using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>
    /// Owner 2026-11-02: a patrol is assigned to one frontier - Emain, Odin or Hadrian's Wall - and finishes inside it;
    /// only a new task may name another frontier, and that crossing is by the teleporter.
    /// </summary>
    [TestFixture]
    public class UT_RvrPatrolRegion
    {
        [Test]
        public void OnlyTheThreeFrontiersCanCarryAPatrol()
        {
            Assert.That(AutonomousRvrPatrolRegion.IsPatrolRegion(1), Is.True);
            Assert.That(AutonomousRvrPatrolRegion.IsPatrolRegion(100), Is.True);
            Assert.That(AutonomousRvrPatrolRegion.IsPatrolRegion(200), Is.True);
            Assert.That(AutonomousRvrPatrolRegion.IsPatrolRegion(10), Is.False);
            Assert.That(AutonomousRvrPatrolRegion.IsPatrolRegion(101), Is.False);
            Assert.That(AutonomousRvrPatrolRegion.IsPatrolRegion(250), Is.False);
            Assert.That(AutonomousRvrPatrolRegion.IsPatrolRegion(0), Is.False);
        }

        [Test]
        public void MidTaskThePartyIsPinnedToItsAssignedFrontier()
        {
            Assert.That(AutonomousRvrPatrolRegion.PinsToAssignedRegion(200, true, false), Is.True);
            Assert.That(AutonomousRvrPatrolRegion.PinsToAssignedRegion(100, true, false), Is.True);
            Assert.That(AutonomousRvrPatrolRegion.PinsToAssignedRegion(1, true, false), Is.True);
        }

        [Test]
        public void ChoosingANewObjectiveReleasesThePin()
        {
            Assert.That(AutonomousRvrPatrolRegion.PinsToAssignedRegion(200, true, true), Is.False);
        }

        [Test]
        public void WithoutAnAssignmentOrAReachableOneNothingIsPinned()
        {
            Assert.That(AutonomousRvrPatrolRegion.PinsToAssignedRegion(0, true, false), Is.False);
            Assert.That(AutonomousRvrPatrolRegion.PinsToAssignedRegion(200, false, false), Is.False);
        }

        [Test]
        public void ThePhaseThePartyPicksItsObjectiveInIsTheNameTheCoordinatorWrites()
        {
            Assert.That(AutonomousRvrPatrolRegion.ChoosingTaskPhase, Is.EqualTo("Choosing group target"));
        }
    }
}
