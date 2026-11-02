using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>
    /// Owner 2026-11-02: a frontier patrol read "the party clears dungeon threats before advancing" while walking Emain
    /// in the open. The wording follows what the party is really doing.
    /// </summary>
    [TestFixture]
    public class UT_GroupStatusText
    {
        [Test]
        public void AFrontierPatrolSaysItPatrolsAndNeverMentionsDungeons()
        {
            string text = AutonomousGroupStatusText.Formation("Bererishelm", eAutonomousObjectiveKind.RvR, false);
            Assert.That(text, Is.EqualTo("Keeping formation with Bererishelm; the party patrols the frontier as one"));
            Assert.That(text, Does.Not.Contain("dungeon"));
            // A frontier camp is never a dungeon camp, but the wording must not depend on that: it is RvR either way.
            Assert.That(AutonomousGroupStatusText.Formation("X", eAutonomousObjectiveKind.RvR, true), Does.Not.Contain("dungeon"));
        }

        [Test]
        public void ADungeonCampKeepsItsOwnWording()
        {
            Assert.That(AutonomousGroupStatusText.Formation("X", eAutonomousObjectiveKind.GroupPve, true),
                Is.EqualTo("Keeping formation with X; the party clears dungeon threats before advancing"));
        }

        [Test]
        public void AnOrdinaryPartyAdvancesToItsNextObjective()
        {
            Assert.That(AutonomousGroupStatusText.Formation("X", eAutonomousObjectiveKind.GroupPve, false),
                Is.EqualTo("Keeping formation with X; the party advances to its next objective"));
        }

        [Test]
        public void ThePhaseCarriesTheSameWordsAsTheActivity()
        {
            Assert.That(AutonomousGroupStatusText.TravelingInFormation, Is.EqualTo("Traveling in group formation"));
        }
    }
}
