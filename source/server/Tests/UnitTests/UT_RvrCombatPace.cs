using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>
    /// Owner 2026-11-02: "se un membro del gruppo entra in combat tutto il gruppo si ferma, il leader riparte solo
    /// quando tutto il gruppo è fuori combat". Combat outranks distance, and the resume radius (300 compact) keeps the
    /// leader from walking off before the freed members are back in formation.
    /// </summary>
    [TestFixture]
    public class UT_RvrCombatPace
    {
        [Test]
        public void AMemberInAFightStopsThePartyEvenWhenEveryoneIsClose()
        {
            Assert.That(AutonomousGroupPace.Decide(0f, waiting: false, compact: true, partyFighting: true),
                Is.EqualTo(AutonomousGroupPace.Decision.Wait));
            Assert.That(AutonomousGroupPace.Decide(120f, waiting: false, compact: true, partyFighting: true),
                Is.EqualTo(AutonomousGroupPace.Decision.Wait));
        }

        [Test]
        public void WithNobodyFightingTheCompactRadiiDecide()
        {
            Assert.That(AutonomousGroupPace.Decide(300f, waiting: false, compact: true),
                Is.EqualTo(AutonomousGroupPace.Decision.FullSpeed));
            Assert.That(AutonomousGroupPace.Decide(601f, waiting: false, compact: true),
                Is.EqualTo(AutonomousGroupPace.Decision.Wait));
        }

        [Test]
        public void AfterAFightTheLeaderWaitsUntilEveryoneIsWithinTheResumeRadius()
        {
            // The hold left `waiting` set, so the resume radius (300) applies, not the 600 pause radius.
            Assert.That(AutonomousGroupPace.Decide(450f, waiting: true, compact: true),
                Is.EqualTo(AutonomousGroupPace.Decision.Wait));
            Assert.That(AutonomousGroupPace.Decide(299f, waiting: true, compact: true),
                Is.EqualTo(AutonomousGroupPace.Decision.FullSpeed));
        }

        [Test]
        public void TheRadiiAreTheOwnersNumbers()
        {
            Assert.That(AutonomousGroupPace.CompactTrailRadius, Is.EqualTo(600));
            Assert.That(AutonomousGroupPace.CompactResumeRadius, Is.EqualTo(300));
        }
    }
}
