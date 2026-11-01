using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>
    /// Owner 2026-11-01: "si forma il gruppo, ognuno raggiunge con i suoi tempi e autonomamente il punto d'incontro, da lì
    /// si parte seguendo il capogruppo." So a member crosses a frontier alone only while the party is still converging on
    /// its rendezvous - never after it is assembled, because between two frontiers there is no walk back and the party
    /// stays split for the rest of the run.
    /// </summary>
    [TestFixture]
    public class UT_GroupCrossingRule
    {
        [Test]
        public void AMemberMayCrossWhileTheLeaderIsStaging()
        {
            Assert.That(AutonomousBotGroupCoordinator.MemberMayCrossAlone("Leader staging"), Is.True);
        }

        [Test]
        public void AMemberMayCrossWhileThePartyIsMeetingUp()
        {
            Assert.That(AutonomousBotGroupCoordinator.MemberMayCrossAlone("Meeting up"), Is.True);
        }

        [Test]
        public void AnAssembledPartyCrossesOnlyBehindItsLeader()
        {
            Assert.That(AutonomousBotGroupCoordinator.MemberMayCrossAlone("Traveling"), Is.False);
            Assert.That(AutonomousBotGroupCoordinator.MemberMayCrossAlone("Grinding"), Is.False);
            Assert.That(AutonomousBotGroupCoordinator.MemberMayCrossAlone("Siege in progress"), Is.False);
            Assert.That(AutonomousBotGroupCoordinator.MemberMayCrossAlone("Choosing group target"), Is.False);
        }

        [Test]
        public void AnUnknownOrMissingPhaseIsTreatedAsAssembled()
        {
            Assert.That(AutonomousBotGroupCoordinator.MemberMayCrossAlone(null), Is.False);
            Assert.That(AutonomousBotGroupCoordinator.MemberMayCrossAlone(string.Empty), Is.False);
            Assert.That(AutonomousBotGroupCoordinator.MemberMayCrossAlone("A phase added later"), Is.False);
        }
    }
}
